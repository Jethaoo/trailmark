using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Maui.Devices.Sensors;
using Trailmark.Models;
using Trailmark.Services;

namespace Trailmark.ViewModels;

public enum MapAction { Recenter, ZoomToTrack }
public sealed class MapActionMessage(MapAction value) : ValueChangedMessage<MapAction>(value);
public sealed class MapLocationMessage(Location value) : ValueChangedMessage<Location>(value);

public partial class MapViewModel(
    ITrackService trackService,
    IMapService mapService,
    ILocationService locationService,
    ISettingsService settingsService) : BaseViewModel, IQueryAttributable
{
    private long trackId;
    private string loadedStyleId = string.Empty;

    [ObservableProperty] private string name = "Track map";
    [ObservableProperty] private string distance = "—";
    [ObservableProperty] private string ascent = "—";
    [ObservableProperty] private string duration = "—";
    [ObservableProperty] private string highestPoint = "—";
    [ObservableProperty] private string waypointCount = "0";
    [ObservableProperty] private HtmlWebViewSource mapSource = new();
    [ObservableProperty] private bool isPushedMap;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SheetHeight))]
    private bool isSheetExpanded;

    public double SheetHeight => IsSheetExpanded ? 350 : 218;
    [ObservableProperty] private string locationStatus = "GPS";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("trackId", out object? value) && long.TryParse(value?.ToString(), out long id))
        {
            trackId = id;
            IsPushedMap = true;
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    private Task LoadAsync() => RunBusyAsync(async () =>
    {
        if (trackId == 0)
        {
            DashboardSummary dashboard = await trackService.GetDashboardAsync();
            trackId = dashboard.LastOpened?.Id ?? dashboard.RecentTracks.FirstOrDefault()?.Id ?? 0;
        }

        if (trackId == 0)
            throw new InvalidOperationException("Import a GPX track and it will appear here on the map.");

        Track? track = await trackService.GetTrackAsync(trackId);
        if (track is null)
            throw new InvalidOperationException("This track could not be found.");

        AppSettings settings = await settingsService.GetAsync();
        ApplyTrack(track, settings);
        loadedStyleId = MapStyles.Normalize(settings.MapStyleId);
        MapSource = new HtmlWebViewSource { Html = await mapService.CreateTrackMapHtmlAsync(trackId, loadedStyleId) };
    });

    /// <summary>
    /// Picks up unit or map-style changes made elsewhere without rebuilding the
    /// map (and re-downloading tiles) unless the style actually changed.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (trackId == 0 || MapSource.Html is null)
            return;

        try
        {
            Track? track = await trackService.GetTrackAsync(trackId);
            if (track is null)
                return;

            AppSettings settings = await settingsService.GetAsync();
            ApplyTrack(track, settings);

            string styleId = MapStyles.Normalize(settings.MapStyleId);
            if (!string.Equals(styleId, loadedStyleId, StringComparison.Ordinal))
            {
                loadedStyleId = styleId;
                MapSource = new HtmlWebViewSource { Html = await mapService.CreateTrackMapHtmlAsync(trackId, styleId) };
            }
        }
        catch (Exception)
        {
            // Keep whatever was on screen; a failed refresh should not blank the map.
        }
    }

    private void ApplyTrack(Track track, AppSettings settings)
    {
        Name = track.Name;
        Distance = TrackCardViewModel.FormatDistance(track.DistanceMeters ?? 0d, settings.DistanceUnit);
        Ascent = TrackCardViewModel.FormatElevation(track.ElevationGainMeters, settings.ElevationUnit);
        Duration = track.Duration is { } time ? TrackCardViewModel.FormatDuration(time) : "—";
        HighestPoint = TrackCardViewModel.FormatElevation(track.MaxElevationMeters, settings.ElevationUnit);
        WaypointCount = track.WaypointCount.ToString();
    }

    [RelayCommand]
    private void ToggleSheet() => IsSheetExpanded = !IsSheetExpanded;

    [RelayCommand]
    private static void ZoomToTrack() => WeakReferenceMessenger.Default.Send(new MapActionMessage(MapAction.ZoomToTrack));

    [RelayCommand]
    private async Task RecenterAsync()
    {
        try
        {
            LocationResult result = await locationService.GetCurrentLocationAsync(cancellationToken: default);
            if (!result.IsSuccess || result.Latitude is null || result.Longitude is null)
            {
                LocationStatus = result.Status == Trailmark.Models.LocationStatus.PermissionDenied
                    ? "Permission denied"
                    : "GPS unavailable";
                return;
            }
            LocationStatus = "GPS";
            WeakReferenceMessenger.Default.Send(new MapLocationMessage(
                new Location(result.Latitude.Value, result.Longitude.Value)));
        }
        catch (PermissionException)
        {
            LocationStatus = "Permission denied";
        }
        catch (Exception)
        {
            LocationStatus = "GPS unavailable";
        }
    }

    [RelayCommand]
    private async Task MoreAsync()
    {
        if (Shell.Current is null)
            return;

        const string zoom = "Zoom to track";
        const string recenter = "Recenter on my location";
        const string details = "Open track details";
        const string share = "Share GPX";
        const string close = "Close map";

        string? choice = await Shell.Current.DisplayActionSheetAsync(Name, "Cancel", null, zoom, recenter, details, share, close);
        switch (choice)
        {
            case zoom:
                ZoomToTrack();
                break;
            case recenter:
                await RecenterAsync();
                break;
            case details when trackId != 0:
                await Shell.Current.GoToAsync($"track-details?trackId={trackId}");
                break;
            case share when trackId != 0:
                await RunBusyAsync(() => trackService.ShareAsync(trackId));
                break;
            case close:
                await BackAsync();
                break;
        }
    }

    [RelayCommand]
    private static Task BackAsync()
    {
        if (Shell.Current is null)
            return Task.CompletedTask;

        return Shell.Current.Navigation.NavigationStack.Count > 1
            ? Shell.Current.GoToAsync("..")
            : Shell.Current.GoToAsync("//home");
    }
}
