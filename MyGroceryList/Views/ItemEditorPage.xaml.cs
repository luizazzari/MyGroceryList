using MyGroceryList.Mobile.ViewModels;
using MyGroceryList.Mobile.Services;

namespace MyGroceryList.Mobile.Views;

public partial class ItemEditorPage : ContentPage, IQueryAttributable
{
    private readonly ItemEditorViewModel viewModel;
    private readonly ThemeService themeService;
    public ItemEditorPage(ItemEditorViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
        this.themeService = themeService;
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        viewModel.ListId = (Guid)query["ListId"];
        viewModel.ItemId = (Guid)query["ItemId"];
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void ShowOptions(object? sender, EventArgs e)
    {
        var selected = await DisplayActionSheetAsync("Opções do item", "Cancelar", "Excluir item", "Aparência");
        if (selected == "Excluir item") await viewModel.DeleteCommand.ExecuteAsync(null);
        else if (selected == "Aparência") await themeService.ChooseAsync(this);
    }
}
