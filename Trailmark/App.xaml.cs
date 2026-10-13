using Trailmark.Models;
using Trailmark.Services;
using Trailmark.Views;

namespace Trailmark;

public partial class App : Application
{
    private readonly AppShell shell;
    private readonly ISettingsService settingsService;

    public App(AppShell shell, ISettingsService settingsService)
    {
        InitializeComponent();
        this.shell = shell;
        this.settingsService = settingsService;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(shell);
        _ = RestoreSavedThemeAsync();
        return window;
    }

    /// <summary>Applies the persisted theme preference so it survives a restart.</summary>
    private async Task RestoreSavedThemeAsync()
    {
        try
        {
            AppSettings settings = await settingsService.GetAsync();
            ApplyTheme(settings.Theme);
        }
        catch (Exception)
        {
            // Falling back to the system theme keeps the app usable if storage is unavailable.
        }
    }

    public static void ApplyTheme(ThemePreference preference)
    {
        if (Current is null)
            return;

        Current.UserAppTheme = preference switch
        {
            ThemePreference.Light => AppTheme.Light,
            ThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }
}
