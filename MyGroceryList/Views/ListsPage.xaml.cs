using MyGroceryList.Mobile.ViewModels;
using MyGroceryList.Mobile.Services;

namespace MyGroceryList.Mobile.Views;

public partial class ListsPage : ContentPage
{
    private readonly ListsViewModel viewModel;
    private readonly ThemeService themeService;
    public ListsPage(ListsViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        this.themeService = themeService;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void ShowOptions(object? sender, EventArgs e)
    {
        var archiveOption = viewModel.SwitchLabel;
        var selected = await DisplayActionSheetAsync("Opções", "Cancelar", null, "Importar lista", archiveOption, "Aparência");
        if (selected == archiveOption) await viewModel.SwitchArchiveCommand.ExecuteAsync(null);
        else if (selected == "Importar lista") await viewModel.ImportCommand.ExecuteAsync(null);
        else if (selected == "Aparência") await themeService.ChooseAsync(this);
    }
}
