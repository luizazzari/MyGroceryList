using MyGroceryList.Mobile.ViewModels;
using MyGroceryList.Mobile.Services;

namespace MyGroceryList.Mobile.Views;

public partial class ShoppingPage : ContentPage, IQueryAttributable
{
    private readonly ShoppingViewModel viewModel;
    private readonly ThemeService themeService;
    public ShoppingPage(ShoppingViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        this.themeService = themeService;
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query) => viewModel.ListId = (Guid)query["ListId"];
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        viewModel.EntryAdded += OnEntryAdded;
        await viewModel.LoadCommand.ExecuteAsync(null);
    }
    protected override void OnDisappearing()
    {
        viewModel.EntryAdded -= OnEntryAdded;
        base.OnDisappearing();
    }
    private void OnEntryAdded(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(() => QuickEntryField.Focus());
        SemanticScreenReader.Default.Announce(viewModel.StatusMessage);
    }
    private void FocusQuickEntry(object? sender, EventArgs e)
    {
        QuickEntryPanel.IsVisible = true;
        OpenQuickEntryButton.IsVisible = false;
        Dispatcher.Dispatch(() => QuickEntryField.Focus());
    }

    private async void CloseQuickEntry(object? sender, EventArgs e)
    {
        await QuickEntryField.HideSoftInputAsync(CancellationToken.None);
        QuickEntryField.Unfocus();
        QuickEntryPanel.IsVisible = false;
        OpenQuickEntryButton.IsVisible = true;
    }

    private async void ShowOptions(object? sender, EventArgs e)
    {
        var selected = await DisplayActionSheetAsync("Opções da lista", "Cancelar", null,
            "Renomear lista", "Arquivar lista", "Aparência");
        switch (selected)
        {
            case "Renomear lista": await viewModel.RenameCommand.ExecuteAsync(null); break;
            case "Arquivar lista": await viewModel.ArchiveCommand.ExecuteAsync(null); break;
            case "Aparência": await themeService.ChooseAsync(this); break;
        }
    }
}
