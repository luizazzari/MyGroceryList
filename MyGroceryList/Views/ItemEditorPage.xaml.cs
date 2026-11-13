using MyGroceryList.Mobile.ViewModels;

namespace MyGroceryList.Mobile.Views;

public partial class ItemEditorPage : ContentPage, IQueryAttributable
{
    private readonly ItemEditorViewModel viewModel;
    public ItemEditorPage(ItemEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
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
}
