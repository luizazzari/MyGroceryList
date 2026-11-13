namespace MyGroceryList.Domain.Shopping;

public sealed class ShoppingValidationException(string message) : Exception(message);

public sealed record ItemDraft(string Name, decimal? Quantity = null, string? Unit = null,
    string Sector = Sectors.Other, string? Note = null)
{
    public ItemDraft Validated()
    {
        var name = Name.Trim();
        if (name.Length is < 1 or > 160)
            throw new ShoppingValidationException("Informe um produto com até 160 caracteres.");
        if (Quantity is <= 0)
            throw new ShoppingValidationException("A quantidade deve ser maior que zero.");
        if (Unit?.Trim().Length > 24)
            throw new ShoppingValidationException("A unidade deve ter até 24 caracteres.");
        if (Note?.Trim().Length > 500)
            throw new ShoppingValidationException("A observação deve ter até 500 caracteres.");
        return this with { Name = name, Unit = ProductText.NormalizeUnit(Unit),
            Sector = Sectors.Validate(Sector), Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim() };
    }
}

public sealed record GroceryItem(Guid Id, ItemDraft Details, bool IsPurchased, long Position)
{
    public bool CanMerge(ItemDraft other) => !IsPurchased && Details.Quantity.HasValue &&
        other.Quantity.HasValue && ProductText.Key(Details.Name) == ProductText.Key(other.Name) &&
        ProductText.NormalizeUnit(Details.Unit) == ProductText.NormalizeUnit(other.Unit) &&
        Details.Note == other.Note;
}

public sealed record LastMark(Guid ItemId, bool PreviousValue);

public sealed record GroceryList(Guid Id, string Name, DateTimeOffset CreatedAt,
    bool IsArchived, IReadOnlyList<GroceryItem> Items, LastMark? Undo = null)
{
    public static string ValidateName(string name)
    {
        name = name.Trim();
        return name.Length is > 0 and <= 100 ? name
            : throw new ShoppingValidationException("Informe um nome para a lista com até 100 caracteres.");
    }

    public GroceryList Rename(string name) => this with { Name = ValidateName(name) };

    public GroceryList Add(ItemDraft draft, Guid? mergeInto = null)
    {
        EnsureActive();
        draft = draft.Validated();
        if (mergeInto is { } id)
        {
            var existing = Find(id);
            if (!existing.CanMerge(draft))
                throw new ShoppingValidationException("Não é possível somar estes itens. Mantenha-os separados.");
            return Replace(existing with { Details = existing.Details with
                { Quantity = checked(existing.Details.Quantity!.Value + draft.Quantity!.Value) } });
        }
        var position = Items.Count == 0 ? 0 : Items.Max(i => i.Position) + 1;
        return this with { Items = Items.Append(new GroceryItem(Guid.NewGuid(), draft, false, position)).ToArray() };
    }

    public GroceryList Edit(Guid id, ItemDraft draft)
    {
        EnsureActive();
        return Replace(Find(id) with { Details = draft.Validated() });
    }

    public GroceryList Remove(Guid id)
    {
        EnsureActive();
        _ = Find(id);
        return this with { Items = Items.Where(i => i.Id != id).ToArray(), Undo = Undo?.ItemId == id ? null : Undo };
    }

    public GroceryList SetPurchased(Guid id, bool desired)
    {
        EnsureActive();
        var item = Find(id);
        return item.IsPurchased == desired ? this
            : Replace(item with { IsPurchased = desired }) with { Undo = new(id, item.IsPurchased) };
    }

    public GroceryList UndoLastMark()
    {
        EnsureActive();
        if (Undo is not { } undo) return this;
        return Replace(Find(undo.ItemId) with { IsPurchased = undo.PreviousValue }) with { Undo = null };
    }

    private GroceryItem Find(Guid id) => Items.FirstOrDefault(i => i.Id == id)
        ?? throw new ShoppingValidationException("Este item não está mais na lista.");
    private GroceryList Replace(GroceryItem item) => this with
        { Items = Items.Select(i => i.Id == item.Id ? item : i).ToArray() };
    private void EnsureActive()
    {
        if (IsArchived) throw new ShoppingValidationException("Restaure a lista antes de alterar seus itens.");
    }
}
