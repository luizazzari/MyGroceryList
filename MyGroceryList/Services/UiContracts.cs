using MyGroceryList.Application.Transfer;

namespace MyGroceryList.Mobile.Services;

public interface IUserInteraction
{
    Task<string?> PromptAsync(string title, string message, string? initialValue = null);
    Task<bool> ConfirmAsync(string title, string message, string accept = "Confirmar");
    Task<string?> ChooseAsync(string title, params string[] options);
    void ReportFailure(Exception exception);
}

public interface IAppNavigation
{
    Task OpenListAsync(Guid id);
    Task EditItemAsync(Guid listId, Guid itemId);
    Task BackAsync();
    Task ReviewImportAsync(ListImportPreview preview);
    Task OpenImportedListAsync(Guid id);
}

public interface IListFiles
{
    Task<ListFile?> PickAsync(CancellationToken cancellationToken = default);
    Task ShareAsync(ListFile file, CancellationToken cancellationToken = default);
}

public interface IUiDispatcher
{
    Task InvokeAsync(Action action);
}
