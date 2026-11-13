using System.Globalization;
using System.Text.RegularExpressions;
using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Application.Shopping;

public static partial class QuickEntryParser
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

    public static ItemDraft Parse(string input)
    {
        var text = input.Trim();
        var match = LeadingQuantity().Match(text);
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value,
                NumberStyles.AllowDecimalPoint, Portuguese, out var quantity) || quantity <= 0)
            return new ItemDraft(text).Validated();

        var rest = match.Groups[2].Value;
        var unit = LeadingUnit().Match(rest);
        return (unit.Success
            ? new ItemDraft(unit.Groups[2].Value, quantity, unit.Groups[1].Value)
            : new ItemDraft(rest, quantity)).Validated();
    }

    // Only a quantity at the start is interpreted. Packaging suffixes remain in the product name.
    [GeneratedRegex(@"^([0-9]+(?:,[0-9]+)?)\s+(.+)$")]
    private static partial Regex LeadingQuantity();

    [GeneratedRegex(@"^(kg|g|ml|l|un|quilos?|quilogramas?|gramas?|litros?|mililitros?|unidades?)\s+(?:de\s+)?(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingUnit();

    public static decimal? ParseQuantity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!QuantityOnly().IsMatch(text.Trim()) || !decimal.TryParse(text.Trim(),
            NumberStyles.AllowDecimalPoint, Portuguese, out var value) || value <= 0)
            throw new ShoppingValidationException("Informe uma quantidade positiva, como 1,5, ou deixe em branco.");
        return value;
    }

    [GeneratedRegex(@"^[0-9]+(?:,[0-9]+)?$")]
    private static partial Regex QuantityOnly();
}
