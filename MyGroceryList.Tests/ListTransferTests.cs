using System.Text;
using Microsoft.Data.Sqlite;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Application.Transfer;
using MyGroceryList.Domain.Shopping;
using MyGroceryList.Mobile.Data.Shopping;

namespace MyGroceryList.Tests;

public sealed class ListTransferTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "MyGroceryList.Tests", Guid.NewGuid().ToString());
    private string DatabasePath => Path.Combine(directory, "transfer.db");
    private SqliteShoppingRepository Repository => new(DatabasePath);
    private ListTransferService Transfer => new(Repository, new FixedTime());
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ListFileFormat.Json)]
    [InlineData(ListFileFormat.Csv)]
    public async Task Export_import_round_trip_preserves_content_and_creates_independent_ids(ListFileFormat format)
    {
        var source = new GroceryList(Guid.NewGuid(), "Compra; \"sábado\"", DateTimeOffset.UtcNow, true,
        [
            new(Guid.NewGuid(), new ItemDraft("Queijo; minas \"leve\"", 0.1234567890123456789012345678m,
                "kg", "Laticínios", "Marca A, se houver\r\nSenão, marca B").Validated(), true, 0),
            new(Guid.NewGuid(), new("Leite integral 1 L"), false, 1),
            new(Guid.NewGuid(), new("Leite integral 1 L"), false, 2)
        ]);
        await Repository.CreateAsync(source, Cancellation);
        var exported = await Transfer.ExportAsync(source.Id, format, Cancellation);
        var preview = await Transfer.ReadAsync(exported.FileName, exported.Content, Cancellation);
        Assert.Equal(source.Name, preview.Name);
        var imported = await Transfer.ImportAsync(preview.Name, preview.Items, Cancellation);

        Assert.NotEqual(source.Id, imported.Id);
        Assert.False(imported.IsArchived);
        Assert.Null(imported.Undo);
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero), imported.CreatedAt);
        Assert.Equal(3, imported.Items.Count);
        for (var index = 0; index < source.Items.Count; index++)
        {
            Assert.Equal(source.Items[index].Details, imported.Items[index].Details);
            Assert.Equal(source.Items[index].IsPurchased, imported.Items[index].IsPurchased);
            Assert.NotEqual(source.Items[index].Id, imported.Items[index].Id);
        }
        var reopened = await Repository.GetAsync(imported.Id, Cancellation);
        Assert.Equal(imported.Items, reopened.Items);
        var shopping = new ShoppingService(Repository, TimeProvider.System);
        await shopping.EditAsync(imported.Id, imported.Items[0].Id, new("Produto alterado"), Cancellation);
        Assert.Equal(source.Items[0].Details, (await Repository.GetAsync(source.Id, Cancellation)).Items[0].Details);
        if (format == ListFileFormat.Csv)
        {
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, exported.Content.Take(3));
            Assert.Contains("0,1234567890123456789012345678", Encoding.UTF8.GetString(exported.Content));
        }
    }

    [Theory]
    [InlineData(ListFileFormat.Json)]
    [InlineData(ListFileFormat.Csv)]
    public async Task Empty_lists_preserve_their_name(ListFileFormat format)
    {
        var source = new GroceryList(Guid.NewGuid(), "Lista vazia: sábado", DateTimeOffset.UtcNow, false, []);
        await Repository.CreateAsync(source, Cancellation);
        var exported = await Transfer.ExportAsync(source.Id, format, Cancellation);
        var preview = await Transfer.ReadAsync(exported.FileName, exported.Content, Cancellation);
        Assert.Equal(source.Name, preview.Name);
        Assert.Empty(preview.Items);
        Assert.DoesNotContain(':', exported.FileName);
    }

    [Theory]
    [InlineData("produto;quantidade;unidade;setor;observação;comprado\r\nMaçã;1,5;quilos;HORTIFRUTI;Madura;sim\r\n", "1.5")]
    [InlineData("produto,quantidade,unidade,setor,observação,comprado\nMaçã,\"1,5\",quilos,HORTIFRUTI,Madura,true\n", "1.5")]
    [InlineData("produto,quantidade,unidade,setor,observação,comprado\nMaçã,1.5,quilos,HORTIFRUTI,Madura,1\n", "1.5")]
    public async Task External_csv_accepts_both_separators_decimal_formats_and_normalized_headers(string csv, string quantity)
    {
        var preview = await Transfer.ReadAsync("Feira.CSV", Encoding.UTF8.GetBytes(csv), Cancellation);
        Assert.Equal("Feira", preview.Name);
        var item = Assert.Single(preview.Items);
        Assert.Equal(decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture), item.Details.Quantity);
        Assert.Equal("kg", item.Details.Unit);
        Assert.Equal("Hortifruti", item.Details.Sector);
        Assert.True(item.IsPurchased);
    }

    [Fact]
    public async Task Minimal_csv_and_utf16_bom_are_supported()
    {
        const string csv = "nome\r\nPão\r\n\r\nCafé\r\n";
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(csv)).ToArray();
        var preview = await Transfer.ReadAsync("Padaria.csv", bytes, Cancellation);
        Assert.Equal(2, preview.Items.Count);
        Assert.Equal("Pão", preview.Items[0].Details.Name);
        Assert.Null(preview.Items[0].Details.Quantity);
        Assert.Equal("Outros", preview.Items[0].Details.Sector);
        Assert.False(preview.Items[0].IsPurchased);
    }

    [Theory]
    [InlineData("nome;quantidade\nArroz;-2\n")]
    [InlineData("nome;quantidade\nArroz;1.000,5\n")]
    [InlineData("nome;quantidade\nArroz;2;extra\n")]
    [InlineData("nome;nome\nArroz;Feijão\n")]
    [InlineData("produto;nome\nArroz;Feijão\n")]
    [InlineData("quantidade\n2\n")]
    [InlineData("nome;comprado\nArroz;talvez\n")]
    [InlineData("nome;setor\nArroz;Desconhecido\n")]
    [InlineData("nome\n\"Produto sem fechar\n")]
    [InlineData("nome\n\"Produto\"texto\n")]
    [InlineData("nome\nPro\"duto\n")]
    [InlineData("lista;nome\nA;Arroz\nB;Feijão\n")]
    [InlineData("nome;quantidade\n;2\n")]
    public async Task Invalid_csv_is_rejected_before_persistence(string csv)
    {
        await Assert.ThrowsAsync<ShoppingValidationException>(() => Transfer.ReadAsync("Lista.csv", Encoding.UTF8.GetBytes(csv), Cancellation));
        Assert.Empty(await Repository.GetListsAsync(false, Cancellation));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"format\":\"MyGroceryList\",\"version\":2,\"name\":\"Lista\",\"items\":[]}")]
    [InlineData("{\"format\":\"MyGroceryList\",\"version\":1,\"name\":\"Lista\",\"items\":[null]}")]
    [InlineData("{\"format\":\"MyGroceryList\",\"version\":1,\"name\":\"Lista\",\"items\":[{\"name\":\"Arroz\",\"quantity\":0}]}")]
    public async Task Invalid_or_incompatible_json_is_rejected(string json) =>
        await Assert.ThrowsAsync<ShoppingValidationException>(() => Transfer.ReadAsync("lista.json", Encoding.UTF8.GetBytes(json), Cancellation));

    [Fact]
    public async Task Import_rolls_back_the_whole_batch_when_a_database_write_fails()
    {
        var original = await Transfer.ImportAsync("Existente", [], Cancellation);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER FailImportedItem BEFORE INSERT ON ShoppingItems WHEN NEW.Name='Falhar'
                BEGIN SELECT RAISE(ABORT, 'Simulated failure'); END;
                """;
            command.ExecuteNonQuery();
        }
        await Assert.ThrowsAsync<SqliteException>(() => Transfer.ImportAsync("Importada",
            [new(new("Primeiro"), false), new(new("Falhar"), true)], Cancellation));
        Assert.Equal(original.Id, Assert.Single(await Repository.GetListsAsync(false, Cancellation)).Id);
    }

    [Fact]
    public async Task Validation_or_cancellation_does_not_save_part_of_a_list()
    {
        await Assert.ThrowsAsync<ShoppingValidationException>(() => Transfer.ImportAsync("Importada",
            [new(new("Válido"), false), new(new("Inválido", -1m), false)], Cancellation));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Transfer.ImportAsync("Cancelada",
            [new(new("Arroz"), false)], cancellation.Token));
        Assert.Empty(await Repository.GetListsAsync(false, Cancellation));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);
    }
}
