using System.Globalization;

namespace MyGroceryList.Mobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider services;
    public App(IServiceProvider services)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        InitializeComponent();
        this.services = services;
    }
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(services.GetRequiredService<AppShell>());
}
