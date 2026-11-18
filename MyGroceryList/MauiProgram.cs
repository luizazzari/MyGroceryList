using Microsoft.Extensions.Logging;
using MyGroceryList.Application.Shopping;
using MyGroceryList.Application.Transfer;
using MyGroceryList.Mobile.Data.Shopping;
using MyGroceryList.Mobile.Services;
using MyGroceryList.Mobile.ViewModels;
using MyGroceryList.Mobile.Views;

namespace MyGroceryList.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IShoppingRepository>(_ => new SqliteShoppingRepository(
            Path.Combine(FileSystem.AppDataDirectory, "mygrocerylist.db")));
        builder.Services.AddSingleton<ShoppingService>();
        builder.Services.AddSingleton<ListTransferService>();
        builder.Services.AddSingleton<IListFiles, MauiListFiles>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<IUserInteraction, MauiUserInteraction>();
        builder.Services.AddSingleton<IAppNavigation, ShellNavigation>();
        builder.Services.AddSingleton<IUiDispatcher, MauiUiDispatcher>();
        builder.Services.AddTransient<ListsViewModel>();
        builder.Services.AddTransient<ShoppingViewModel>();
        builder.Services.AddTransient<ItemEditorViewModel>();
        builder.Services.AddTransient<ImportListViewModel>();
        builder.Services.AddTransient<ListsPage>();
        builder.Services.AddTransient<ShoppingPage>();
        builder.Services.AddTransient<ItemEditorPage>();
        builder.Services.AddTransient<ImportListPage>();
        builder.Services.AddSingleton<AppShell>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
