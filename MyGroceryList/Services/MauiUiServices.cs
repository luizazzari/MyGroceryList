using Microsoft.Extensions.Logging;
using MyGroceryList.Mobile.Views;

namespace MyGroceryList.Mobile.Services;

public sealed class MauiUserInteraction(ILogger<MauiUserInteraction> logger) : IUserInteraction
{
    public async Task<string?> PromptAsync(string title, string message, string? initialValue = null) =>
        await Shell.Current.DisplayPromptAsync(title, message, "Salvar", "Cancelar", initialValue: initialValue, maxLength: 100);

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Confirmar") =>
        Shell.Current.DisplayAlertAsync(title, message, accept, "Cancelar");

    public async Task<string?> ChooseAsync(string title, params string[] options)
    {
        var result = await Shell.Current.DisplayActionSheetAsync(title, "Cancelar", null, options);
        return options.Contains(result) ? result : null;
    }

    public void ReportFailure(Exception exception) =>
        logger.LogError("Falha na operação local. Tipo: {ExceptionType}", exception.GetType().Name);
}

public sealed class ShellNavigation : IAppNavigation
{
    public Task OpenListAsync(Guid id) => Shell.Current.GoToAsync(nameof(ShoppingPage),
        new ShellNavigationQueryParameters { ["ListId"] = id });
    public Task EditItemAsync(Guid listId, Guid itemId) => Shell.Current.GoToAsync(nameof(ItemEditorPage),
        new ShellNavigationQueryParameters { ["ListId"] = listId, ["ItemId"] = itemId });
    public Task BackAsync() => Shell.Current.GoToAsync("..");
}

public sealed class MauiUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Action action) => MainThread.InvokeOnMainThreadAsync(action);
}
