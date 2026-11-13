using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Application.Shopping;

public sealed record ListSummary(Guid Id, string Name, bool IsArchived, int Total, int Purchased);
public sealed record SectorPreference(string ProductKey, string Sector);
public sealed record ListMutation(GroceryList List, SectorPreference? Preference = null);

public interface IShoppingRepository
{
    Task<IReadOnlyList<ListSummary>> GetListsAsync(bool archived, CancellationToken cancellationToken = default);
    Task<GroceryList> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task CreateAsync(GroceryList list, CancellationToken cancellationToken = default);
    Task<GroceryList> UpdateAsync(Guid id, Func<GroceryList, ListMutation> update,
        CancellationToken cancellationToken = default);
    Task<string?> GetPreferredSectorAsync(string productKey, CancellationToken cancellationToken = default);
}
