using System.Text;
using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Application.Transfer;

internal static class CsvRecords
{
    public static char DetectSeparator(string text)
    {
        var quoted = false;
        var commas = 0;
        var semicolons = 0;
        foreach (var character in text)
        {
            if (character == '"') quoted = !quoted;
            if (quoted) continue;
            if (character is '\r' or '\n') break;
            if (character == ',') commas++;
            if (character == ';') semicolons++;
        }
        return semicolons >= commas ? ';' : ',';
    }

    public static IReadOnlyList<string[]> Read(string text, char separator, CancellationToken ct)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closedQuote = false;

        void FinishField()
        {
            fields.Add(field.ToString());
            field.Clear();
            closedQuote = false;
        }

        void FinishRecord()
        {
            FinishField();
            if (fields.Any(value => !string.IsNullOrWhiteSpace(value))) records.Add(fields.ToArray());
            fields.Clear();
        }

        for (var index = 0; index < text.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var character = text[index];
            if (quoted)
            {
                if (character != '"') field.Append(character);
                else if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = false;
                    closedQuote = true;
                }
                continue;
            }

            if (character == separator) FinishField();
            else if (character is '\r' or '\n')
            {
                FinishRecord();
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
            }
            else if (closedQuote)
            {
                if (!char.IsWhiteSpace(character))
                    throw new ShoppingValidationException("CSV inválido. Há texto após o fechamento das aspas.");
            }
            else if (character == '"')
            {
                if (field.Length != 0)
                    throw new ShoppingValidationException("CSV inválido. Coloque entre aspas todo o campo que contém aspas.");
                quoted = true;
            }
            else field.Append(character);
        }
        if (quoted) throw new ShoppingValidationException("CSV inválido. Há um campo com aspas sem fechamento.");
        if (field.Length > 0 || fields.Count > 0 || closedQuote) FinishRecord();
        return records;
    }

    public static string WriteRecord(IEnumerable<string?> values) =>
        string.Join(';', values.Select(value => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"")) + "\r\n";
}
