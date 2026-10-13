using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace Trailmark;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataMimeTypes = ["application/gpx+xml", "application/xml", "text/xml"])]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RefreshSystemBarContrast();
        if (Microsoft.Maui.Controls.Application.Current is { } mauiApplication)
            mauiApplication.RequestedThemeChanged += (_, _) => RefreshSystemBarContrast();
    }

    protected override void OnResume()
    {
        base.OnResume();
        RefreshSystemBarContrast();
    }

    /// <summary>Keeps the status/navigation bar icons readable when the theme is switched.</summary>
    private void RefreshSystemBarContrast()
    {
        if (Window is null)
            return;

        bool useDarkIcons = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Light;
        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is null)
            return;

        controller.AppearanceLightStatusBars = useDarkIcons;
        controller.AppearanceLightNavigationBars = useDarkIcons;
    }
}
