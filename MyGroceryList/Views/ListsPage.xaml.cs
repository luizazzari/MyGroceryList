using MyGroceryList.Mobile.ViewModels;

namespace MyGroceryList.Mobile.Views;

public partial class ListsPage : ContentPage
{
    private readonly ListsViewModel viewModel;
    public ListsPage(ListsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
