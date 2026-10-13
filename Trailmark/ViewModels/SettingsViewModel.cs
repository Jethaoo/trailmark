using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trailmark.Models;
using Trailmark.Services;

namespace Trailmark.ViewModels;

public partial class SettingsViewModel(ISettingsService settingsService) : BaseViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDistanceKilometers))]
    [NotifyPropertyChangedFor(nameof(IsDistanceMiles))]
    private string distanceUnit = "Kilometers";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsElevationMeters))]
    [NotifyPropertyChangedFor(nameof(IsElevationFeet))]
    private string elevationUnit = "Meters";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThemeSystem))]
    [NotifyPropertyChangedFor(nameof(IsThemeLight))]
    [NotifyPropertyChangedFor(nameof(IsThemeDark))]
    private string theme = "System";

    [ObservableProperty] private string mapStyle = "MapTiler Outdoor";
    [ObservableProperty] private string mapStyleDescription = "Terrain, contours and trails";
    [ObservableProperty] private string storageSummary = "0 tracks · 0 MB";
    [ObservableProperty] private double storageFraction;
    [ObservableProperty] private bool hasStorage;

    public bool IsDistanceKilometers => DistanceUnit == "Kilometers";
    public bool IsDistanceMiles => DistanceUnit == "Miles";
    public bool IsElevationMeters => ElevationUnit == "Meters";
    public bool IsElevationFeet => ElevationUnit == "Feet";
    public bool IsThemeSystem => Theme == "System";
    public bool IsThemeLight => Theme == "Light";
    public bool IsThemeDark => Theme == "Dark";

    [RelayCommand]
    private Task LoadAsync() => RunBusyAsync(async () =>
    {
        AppSettings settings = await settingsService.GetAsync();
        DistanceUnit = settings.DistanceUnit == Trailmark.Models.DistanceUnit.Miles ? "Miles" : "Kilometers";
        ElevationUnit = settings.ElevationUnit == Trailmark.Models.ElevationUnit.Feet ? "Feet" : "Meters";
        Theme = settings.Theme.ToString();
        ApplyMapStyle(settings.MapStyleId);
        StorageSummary = $"{settings.TrackCount} {(settings.TrackCount == 1 ? "track" : "tracks")} · {settings.StorageBytes / 1_048_576d:N1} MB";
        StorageFraction = Math.Clamp(settings.StorageBytes / (250d * 1_048_576d), 0, 1);
        HasStorage = settings.StorageBytes > 0;
        App.ApplyTheme(settings.Theme);
    });

    [RelayCommand]
    private Task SetDistanceUnitAsync(string value) => RunBusyAsync(async () =>
    {
        Trailmark.Models.DistanceUnit unit = value == "mi" ? Trailmark.Models.DistanceUnit.Miles : Trailmark.Models.DistanceUnit.Kilometers;
        await settingsService.SetDistanceUnitAsync(unit);
        DistanceUnit = unit == Trailmark.Models.DistanceUnit.Miles ? "Miles" : "Kilometers";
    });

    [RelayCommand]
    private Task SetElevationUnitAsync(string value) => RunBusyAsync(async () =>
    {
        Trailmark.Models.ElevationUnit unit = value == "ft" ? Trailmark.Models.ElevationUnit.Feet : Trailmark.Models.ElevationUnit.Meters;
        await settingsService.SetElevationUnitAsync(unit);
        ElevationUnit = unit == Trailmark.Models.ElevationUnit.Feet ? "Feet" : "Meters";
    });

    [RelayCommand]
    private Task SetThemeAsync(string value) => RunBusyAsync(async () =>
    {
        ThemePreference preference = Enum.Parse<ThemePreference>(value, true);
        await settingsService.SetThemeAsync(preference);
        Theme = preference.ToString();
        App.ApplyTheme(preference);
    });

    [RelayCommand]
    private async Task ChooseMapStyleAsync()
    {
        if (Shell.Current is null)
            return;

        string[] options = MapStyles.All.Select(style => $"{style.Name} — {style.Description}").ToArray();
        string current = MapStyles.NameFor(currentStyleId);
        string? choice = await Shell.Current.DisplayActionSheetAsync("Map style", "Cancel", null, options);
        int index = Array.FindIndex(options, option => option == choice);
        if (index < 0)
            return;

        string styleId = MapStyles.All[index].Id;
        await RunBusyAsync(async () =>
        {
            await settingsService.SetMapStyleAsync(styleId);
            ApplyMapStyle(styleId);
        });

        await Shell.Current.DisplayAlertAsync(
            "Map style updated",
            $"{MapStyles.NameFor(styleId)} is now your default style. Open any track on the map to see it.",
            "OK");
    }

    [RelayCommand]
    private async Task ShowStorageAsync()
    {
        if (Shell.Current is null)
            return;

        AppSettings settings = await settingsService.GetAsync();
        string size = settings.StorageBytes < 1_048_576L
            ? $"{settings.StorageBytes / 1024d:N0} KB"
            : $"{settings.StorageBytes / 1_048_576d:N1} MB";
        await Shell.Current.DisplayAlertAsync(
            "Local GPX files",
            $"{settings.TrackCount} {(settings.TrackCount == 1 ? "track" : "tracks")} stored on this device.\n\n" +
            $"Original GPX files: {size}\nEvery track, its points and its original file stay in the app's private storage and are never uploaded.",
            "Close");
    }

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        if (Shell.Current is null)
            return;

        await Shell.Current.DisplayAlertAsync(
            "Trailmark",
            $"Version {AppInfo.Current.VersionString} (build {AppInfo.Current.BuildString})\n\n" +
            "An offline-first GPX viewer. Import, review and share your tracks without an account.\n\n" +
            "Maps © MapTiler · © OpenStreetMap contributors.",
            "Close");
    }

    private string currentStyleId = MapStyles.Default;

    private void ApplyMapStyle(string? styleId)
    {
        currentStyleId = MapStyles.Normalize(styleId);
        var style = MapStyles.All.First(item => item.Id == currentStyleId);
        MapStyle = style.Name;
        MapStyleDescription = style.Description;
    }
}
