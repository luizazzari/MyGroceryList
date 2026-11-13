using MyGroceryList.Mobile.ViewModels;

namespace MyGroceryList.Mobile.Views;

public partial class ShoppingPage : ContentPage, IQueryAttributable
{
    private readonly ShoppingViewModel viewModel;
    public ShoppingPage(ShoppingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
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
    private void FocusQuickEntry(object? sender, EventArgs e) => QuickEntryField.Focus();
}
