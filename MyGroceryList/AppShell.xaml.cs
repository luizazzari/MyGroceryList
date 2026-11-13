using MyGroceryList.Mobile.Views;

namespace MyGroceryList.Mobile;

public partial class AppShell : Shell
{
    public AppShell(ListsPage listsPage)
    {
        InitializeComponent();
        ListsContent.Content = listsPage;
        Routing.RegisterRoute(nameof(ShoppingPage), typeof(ShoppingPage));
        Routing.RegisterRoute(nameof(ItemEditorPage), typeof(ItemEditorPage));
    }
}
