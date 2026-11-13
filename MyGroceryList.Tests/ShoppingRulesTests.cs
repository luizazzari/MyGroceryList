using MyGroceryList.Application.Shopping;
using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Tests;

public class ShoppingRulesTests
{
    [Theory]
    [InlineData("2 leites", "leites", "2", null)]
    [InlineData("500 g de queijo", "queijo", "500", "g")]
    [InlineData("1,5 kg de arroz", "arroz", "1.5", "kg")]
    [InlineData("Leite integral 1 L", "Leite integral 1 L", null, null)]
    [InlineData("2.5 kg de arroz", "2.5 kg de arroz", null, null)]
    [InlineData("0 leites", "0 leites", null, null)]
    [InlineData("2 litros de suco", "suco", "2", "L")]
    public void Quick_entry_preserves_names_and_only_parses_unambiguous_prefixes(string text, string name,
        string? quantity, string? unit)
    {
        var parsed = QuickEntryParser.Parse(text);
        Assert.Equal(name, parsed.Name);
        Assert.Equal(quantity is null ? null : decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture), parsed.Quantity);
        Assert.Equal(unit, parsed.Unit);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("1.000")]
    [InlineData("0")]
    [InlineData("texto")]
    public void Invalid_editor_quantity_is_rejected(string input) =>
        Assert.Throws<ShoppingValidationException>(() => QuickEntryParser.ParseQuantity(input));

    [Fact]
    public void Quantities_use_decimal_comma_and_are_optional()
    {
        Assert.Equal(1.5m, QuickEntryParser.ParseQuantity("1,5"));
        Assert.Null(QuickEntryParser.ParseQuantity(" "));
    }

    [Fact]
    public void Only_explicit_quantities_of_equivalent_products_and_units_can_merge()
    {
        var list = NewList().Add(new("Maçã", 1.5m, "quilos", "Hortifruti"));
        var id = list.Items[0].Id;
        var merged = list.Add(new("MACA", 2m, "kg", "Hortifruti"), id);
        Assert.Equal(3.5m, Assert.Single(merged.Items).Details.Quantity);
        Assert.Equal("Maçã", merged.Items[0].Details.Name);
        Assert.Throws<ShoppingValidationException>(() => list.Add(new("Maçã", 500m, "g"), id));
        Assert.Throws<ShoppingValidationException>(() => list.Add(new("Maçã"), id));
        Assert.Throws<ShoppingValidationException>(() => list.Add(new("Maçã verde", 2m, "kg"), id));
        Assert.False(new GroceryItem(Guid.NewGuid(), new("leite integral", 1m, "L"), false, 0)
            .CanMerge(new("leite sem lactose", 1m, "L")));
    }

    [Fact]
    public void Marking_is_idempotent_and_undo_restores_the_previous_state_once()
    {
        var list = NewList().Add(new("Pão"));
        var id = list.Items[0].Id;
        list = list.SetPurchased(id, true);
        list = list.SetPurchased(id, true);
        Assert.True(list.Items[0].IsPurchased);
        list = list.UndoLastMark();
        Assert.False(list.Items[0].IsPurchased);
        Assert.Null(list.Undo);
        Assert.Same(list, list.UndoLastMark());
        list = list.SetPurchased(id, true).SetPurchased(id, false).UndoLastMark();
        Assert.True(list.Items[0].IsPurchased);
    }

    [Fact]
    public void Deleting_the_last_marked_item_clears_undo_and_archived_lists_reject_edits()
    {
        var list = NewList().Add(new("Café"));
        var id = list.Items[0].Id;
        Assert.Null(list.SetPurchased(id, true).Remove(id).Undo);
        var archived = list with { IsArchived = true };
        Assert.Throws<ShoppingValidationException>(() => archived.Add(new("Leite")));
        Assert.Throws<ShoppingValidationException>(() => archived.SetPurchased(id, true));
    }

    [Fact]
    public void Validation_rejects_empty_names_invalid_sectors_and_nonpositive_quantities()
    {
        Assert.Throws<ShoppingValidationException>(() => GroceryList.ValidateName(" "));
        Assert.Throws<ShoppingValidationException>(() => new ItemDraft(" ").Validated());
        Assert.Throws<ShoppingValidationException>(() => new ItemDraft("Leite", 0m).Validated());
        Assert.Throws<ShoppingValidationException>(() => new ItemDraft("Leite", Sector: "Inexistente").Validated());
    }

    private static GroceryList NewList() => new(Guid.NewGuid(), "Mercado", DateTimeOffset.UtcNow, false, []);
}
