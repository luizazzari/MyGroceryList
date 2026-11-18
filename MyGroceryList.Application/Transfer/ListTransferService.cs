using System.Globalization;
using System.Text;
using System.Text.Json;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Application.Transfer;

public sealed class ListTransferService(IShoppingRepository repository, TimeProvider timeProvider)
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<ListFile> ExportAsync(Guid listId, ListFileFormat format, CancellationToken ct = default)
    {
        var list = await repository.GetAsync(listId, ct);
        return await Task.Run(() => Encode(list, format, ct), ct);
    }

    public Task<ListImportPreview> ReadAsync(string fileName, byte[] content, CancellationToken ct = default) =>
        Task.Run(() => Decode(fileName, content, ct), ct);

    public async Task<GroceryList> ImportAsync(string name, IReadOnlyList<ImportedItem> items, CancellationToken ct = default)
    {
        var validatedName = GroceryList.ValidateName(name);
        var newItems = new List<GroceryItem>(items.Count);
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            newItems.Add(new GroceryItem(Guid.NewGuid(), item.Details.Validated(), item.IsPurchased, newItems.Count));
        }
        var list = new GroceryList(Guid.NewGuid(), validatedName, timeProvider.GetUtcNow(), false, newItems);
        // CreateAsync saves the list and its entire item batch in one SQLite transaction.
        await repository.CreateAsync(list, ct);
        return list;
    }

    private static ListFile Encode(GroceryList list, ListFileFormat format, CancellationToken ct)
    {
        var fileName = SafeFileName(list.Name);
        var ordered = list.Items.OrderBy(item => item.Position).ToArray();
        if (format == ListFileFormat.Json)
        {
            var payload = new ListFilePayload("MyGroceryList", 1, list.Name, ordered.Select(item =>
                new ListFileItem(item.Details.Name, item.Details.Quantity, item.Details.Unit,
                    item.Details.Sector, item.Details.Note, item.IsPurchased)).ToArray());
            ct.ThrowIfCancellationRequested();
            return new(fileName + ".json", JsonSerializer.SerializeToUtf8Bytes(payload,
                ListFileJsonContext.Default.ListFilePayload), "application/json");
        }
        if (format != ListFileFormat.Csv) throw new ShoppingValidationException("Escolha CSV ou JSON para exportar.");

        var csv = new StringBuilder(CsvRecords.WriteRecord(["lista", "nome", "quantidade", "unidade", "setor", "observacao", "comprado"]));
        foreach (var item in ordered)
        {
            ct.ThrowIfCancellationRequested();
            csv.Append(CsvRecords.WriteRecord([list.Name, item.Details.Name,
                item.Details.Quantity?.ToString(Portuguese), item.Details.Unit, item.Details.Sector,
                item.Details.Note, item.IsPurchased ? "sim" : "não"]));
        }
        if (ordered.Length == 0) csv.Append(CsvRecords.WriteRecord([list.Name, "", "", "", "", "", ""]));
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(csv.ToString())).ToArray();
        return new(fileName + ".csv", bytes, "text/csv");
    }

    private static ListImportPreview Decode(string fileName, byte[] content, CancellationToken ct)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        string text;
        try { text = reader.ReadToEnd(); }
        catch (DecoderFallbackException)
        {
            throw new ShoppingValidationException("Não foi possível ler o texto. Salve o arquivo como CSV UTF-8 ou JSON e tente novamente.");
        }
        ct.ThrowIfCancellationRequested();
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".json" => ReadJson(text),
            ".csv" => ReadCsv(fileName, text, ct),
            _ => throw new ShoppingValidationException("Selecione um arquivo .csv ou .json.")
        };
    }

    private static ListImportPreview ReadJson(string text)
    {
        ListFilePayload? payload;
        try { payload = JsonSerializer.Deserialize(text, ListFileJsonContext.Default.ListFilePayload); }
        catch (JsonException)
        {
            throw new ShoppingValidationException("O JSON está inválido ou contém campos com valores incompatíveis.");
        }
        if (payload is null || payload.Format != "MyGroceryList" || payload.Version != 1 || payload.Items is null)
            throw new ShoppingValidationException("Este arquivo não é uma lista MyGroceryList de versão compatível.");
        var items = payload.Items.Select((item, index) => ValidateItem(
            new(item?.Name ?? "", item?.Quantity, item?.Unit, NormalizeSector(item?.Sector), item?.Note),
            item?.IsPurchased ?? false, index + 1)).ToArray();
        return new(GroceryList.ValidateName(payload.Name ?? ""), items);
    }

    private static ListImportPreview ReadCsv(string fileName, string text, CancellationToken ct)
    {
        var records = CsvRecords.Read(text, CsvRecords.DetectSeparator(text), ct);
        if (records.Count == 0) throw new ShoppingValidationException("O CSV está vazio. Inclua um cabeçalho com a coluna nome ou produto.");
        var columns = new Dictionary<string, int>();
        for (var index = 0; index < records[0].Length; index++)
        {
            var key = ProductText.Key(records[0][index]);
            key = key switch { "produto" => "nome", "nome da lista" => "lista", _ => key };
            if (!columns.TryAdd(key, index)) throw new ShoppingValidationException("O CSV contém colunas repetidas no cabeçalho.");
        }
        if (!columns.ContainsKey("nome")) throw new ShoppingValidationException("O CSV precisa de um cabeçalho com a coluna nome ou produto.");

        string Field(string[] row, string key) => columns.TryGetValue(key, out var index) ? row[index] : "";
        string? listName = null;
        var items = new List<ImportedItem>();
        for (var index = 1; index < records.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var row = records[index];
            if (row.Length != records[0].Length)
                throw new ShoppingValidationException($"O registro {index + 1} tem um número de colunas diferente do cabeçalho.");
            var rowListName = Field(row, "lista").Trim();
            if (rowListName.Length > 0)
            {
                if (listName is not null && listName != rowListName)
                    throw new ShoppingValidationException("O CSV contém mais de uma lista. Use um arquivo por lista.");
                listName = rowListName;
            }
            var name = Field(row, "nome");
            if (string.IsNullOrWhiteSpace(name) && columns.Where(column => column.Key != "lista")
                .All(column => string.IsNullOrWhiteSpace(row[column.Value]))) continue;
            try
            {
                var quantityText = Field(row, "quantidade").Trim();
                decimal? quantity = null;
                if (quantityText.Length > 0)
                {
                    var culture = quantityText.Contains(',') ? Portuguese : CultureInfo.InvariantCulture;
                    if (!decimal.TryParse(quantityText, NumberStyles.AllowDecimalPoint, culture, out var parsed) || parsed <= 0)
                        throw new ShoppingValidationException("Informe uma quantidade positiva, como 1,5.");
                    quantity = parsed;
                }
                var purchased = ProductText.Key(Field(row, "comprado")) switch
                {
                    "" or "nao" or "false" or "0" or "pendente" => false,
                    "sim" or "true" or "1" or "comprado" or "x" => true,
                    _ => throw new ShoppingValidationException("O campo comprado deve ser sim ou não.")
                };
                items.Add(new(new ItemDraft(name, quantity, Field(row, "unidade"),
                    NormalizeSector(Field(row, "setor")), Field(row, "observacao")).Validated(), purchased));
            }
            catch (ShoppingValidationException exception)
            {
                throw new ShoppingValidationException($"Registro {index + 1}: {exception.Message}");
            }
        }
        var fallback = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (fallback.Length == 0) fallback = "Lista importada";
        return new(GroceryList.ValidateName(listName ?? fallback[..Math.Min(fallback.Length, 100)]), items);
    }

    private static ImportedItem ValidateItem(ItemDraft draft, bool purchased, int number)
    {
        try { return new(draft.Validated(), purchased); }
        catch (ShoppingValidationException exception)
        {
            throw new ShoppingValidationException($"Item {number}: {exception.Message}");
        }
    }

    private static string NormalizeSector(string? sector)
    {
        if (string.IsNullOrWhiteSpace(sector)) return Sectors.Other;
        return Sectors.All.FirstOrDefault(value => ProductText.Key(value) == ProductText.Key(sector))
            ?? throw new ShoppingValidationException($"Setor desconhecido: {sector}. Escolha um dos setores do aplicativo.");
    }

    private static string SafeFileName(string name)
    {
        const string invalid = "<>:\"/\\|?*";
        var characters = name.Select(character => char.IsControl(character) || invalid.Contains(character) ? '_' : character).ToArray();
        var result = new string(characters).Trim().TrimEnd('.');
        return "MyGroceryList - " + (result.Length == 0 ? "Minha lista" : result);
    }
}
