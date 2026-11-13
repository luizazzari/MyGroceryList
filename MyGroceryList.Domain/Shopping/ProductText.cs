using System.Globalization;
using System.Text;

namespace MyGroceryList.Domain.Shopping;

public static class ProductText
{
    public static string Key(string value)
    {
        var chars = value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return string.Join(' ', new string(chars.ToArray()).ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string? NormalizeUnit(string? unit) => Key(unit ?? "") switch
    {
        "" => null,
        "quilo" or "quilos" or "quilograma" or "quilogramas" or "kg" => "kg",
        "grama" or "gramas" or "g" => "g",
        "litro" or "litros" or "l" => "L",
        "mililitro" or "mililitros" or "ml" => "mL",
        "unidade" or "unidades" or "un" => "un",
        _ => unit!.Trim()
    };
}
