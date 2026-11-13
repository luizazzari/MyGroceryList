namespace MyGroceryList.Mobile.Services;

public sealed class ThemeService
{
    private const string PreferenceKey = "appearance.theme";

    public void Restore(Microsoft.Maui.Controls.Application application)
    {
        var saved = Preferences.Default.Get(PreferenceKey, (int)AppTheme.Unspecified);
        application.UserAppTheme = Enum.IsDefined(typeof(AppTheme), saved)
            ? (AppTheme)saved : AppTheme.Unspecified;
    }

    public async Task ChooseAsync(Page page)
    {
        var application = Microsoft.Maui.Controls.Application.Current;
        if (application is null) return;
        var current = application.UserAppTheme;
        var options = new (string Name, AppTheme Theme)[]
        {
            ("Automático", AppTheme.Unspecified), ("Claro", AppTheme.Light), ("Escuro", AppTheme.Dark)
        };
        var labels = options.Select(o => o.Theme == current ? $"{o.Name} ✓" : o.Name).ToArray();
        var selected = await page.DisplayActionSheetAsync("Aparência", "Cancelar", null, labels);
        var index = Array.IndexOf(labels, selected);
        if (index < 0) return;
        application.UserAppTheme = options[index].Theme;
        Preferences.Default.Set(PreferenceKey, (int)options[index].Theme);
    }
}
