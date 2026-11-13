using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Application.Shopping;

public sealed class ShoppingService(IShoppingRepository repository, TimeProvider timeProvider)
{
    public Task<IReadOnlyList<ListSummary>> GetListsAsync(bool archived = false, CancellationToken ct = default) =>
        repository.GetListsAsync(archived, ct);
    public Task<GroceryList> OpenAsync(Guid id, CancellationToken ct = default) => repository.GetAsync(id, ct);

    public async Task<GroceryList> CreateAsync(string name, CancellationToken ct = default)
    {
        var list = new GroceryList(Guid.NewGuid(), GroceryList.ValidateName(name),
            timeProvider.GetUtcNow(), false, []);
        await repository.CreateAsync(list, ct);
        return list;
    }

    public async Task<ItemDraft> PrepareEntryAsync(string text, CancellationToken ct = default)
    {
        var draft = QuickEntryParser.Parse(text);
        var sector = await repository.GetPreferredSectorAsync(ProductText.Key(draft.Name), ct);
        return draft with { Sector = sector ?? Sectors.Other };
    }

    public Task<GroceryList> AddAsync(Guid listId, ItemDraft draft, Guid? mergeInto = null, CancellationToken ct = default) =>
        repository.UpdateAsync(listId, list => new(list.Add(draft, mergeInto)), ct);

    public Task<GroceryList> EditAsync(Guid listId, Guid itemId, ItemDraft draft, CancellationToken ct = default) =>
        repository.UpdateAsync(listId, list => new(list.Edit(itemId, draft),
            new(ProductText.Key(draft.Validated().Name), draft.Sector)), ct);

    public Task<GroceryList> RemoveAsync(Guid listId, Guid itemId, CancellationToken ct = default) =>
        repository.UpdateAsync(listId, list => new(list.Remove(itemId)), ct);

    public Task<GroceryList> SetPurchasedAsync(Guid listId, Guid itemId, bool desired, CancellationToken ct = default) =>
        repository.UpdateAsync(listId, list => new(list.SetPurchased(itemId, desired)), ct);

    public Task<GroceryList> UndoAsync(Guid listId, CancellationToken ct = default) =>
        repository.UpdateAsync(listId, list => new(list.UndoLastMark()), ct);

    public Task<GroceryList> RenameAsync(Guid listId, string name, CancellationToken ct = default) =>
        repository.UpdateAsync(listId, list => new(list.Rename(name)), ct);

    public Task<GroceryList> ArchiveAsync(Guid listId, bool archived, CancellationToken ct = default) =>
        repository.UpdateAsync(listId, list => new(list with { IsArchived = archived }), ct);
}
