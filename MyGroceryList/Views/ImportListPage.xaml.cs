using MyGroceryList.Application.Transfer;
using MyGroceryList.Mobile.ViewModels;

namespace MyGroceryList.Mobile.Views;

public partial class ImportListPage : ContentPage, IQueryAttributable
{
    private readonly ImportListViewModel viewModel;

    public ImportListPage(ImportListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        viewModel.Initialize((ListImportPreview)query["Preview"]);
}
