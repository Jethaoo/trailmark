using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trailmark.Models;
using Trailmark.Services;

namespace Trailmark.ViewModels;

public partial class TrackDetailsViewModel(ITrackService trackService, ISettingsService settingsService) : BaseViewModel, IQueryAttributable
{
    private long trackId;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string activityAndDate = string.Empty;
    [ObservableProperty] private string location = string.Empty;
    [ObservableProperty] private string distance = "—";
    [ObservableProperty] private string duration = "—";
    [ObservableProperty] private string gainLoss = "—";
    [ObservableProperty] private string averageSpeed = "—";
    [ObservableProperty] private string highestElevation = "—";
    [ObservableProperty] private string lowestElevation = "—";
    [ObservableProperty] private IReadOnlyList<double> elevationValues = Array.Empty<double>();
    [ObservableProperty] private bool hasTrack;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("trackId", out object? value) && long.TryParse(value?.ToString(), out long id))
        {
            trackId = id;
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    private Task LoadAsync() => RunBusyAsync(async () =>
    {
        Track? track = await trackService.GetTrackAsync(trackId);
        if (track is null)
        {
            ErrorMessage = "This track could not be found.";
            HasTrack = false;
            return;
        }

        AppSettings settings = await settingsService.GetAsync();
        Name = track.Name;
        ActivityAndDate = $"{track.ActivityType.ToUpperInvariant()} · {track.ImportedAt:dd MMM yyyy}";
        Location = string.IsNullOrWhiteSpace(track.Location) ? "Location unavailable" : track.Location;
        Distance = TrackCardViewModel.FormatDistance(track.DistanceMeters ?? 0d, settings.DistanceUnit);
        Duration = track.Duration is { } time ? TrackCardViewModel.FormatDuration(time) : "—";
        GainLoss = $"{TrackCardViewModel.FormatElevation(track.ElevationGainMeters, settings.ElevationUnit)} / {TrackCardViewModel.FormatElevation(track.ElevationLossMeters, settings.ElevationUnit)}";
        AverageSpeed = track.AverageSpeedMetersPerSecond is { } speed
            ? settings.DistanceUnit == DistanceUnit.Miles
                ? $"{speed * 2.236936:N1} mph"
                : $"{speed * 3.6:N1} km/h"
            : "—";
        HighestElevation = track.MaxElevationMeters is { } high
            ? $"{TrackCardViewModel.FormatElevation(high, settings.ElevationUnit)} high"
            : "No elevation data";
        LowestElevation = track.MinElevationMeters is { } low
            ? $"{TrackCardViewModel.FormatElevation(low, settings.ElevationUnit)} low"
            : "—";
        ElevationValues = track.ElevationProfile.Select(sample => sample.ElevationMeters).ToArray();
        HasTrack = true;
    });

    [RelayCommand]
    private Task ViewMapAsync() => Shell.Current.GoToAsync($"map?trackId={trackId}");

    [RelayCommand]
    private Task ShareAsync() => RunBusyAsync(() => trackService.ShareAsync(trackId));

    [RelayCommand]
    private async Task MoreAsync()
    {
        if (Shell.Current is null)
            return;

        const string share = "Share GPX";
        const string map = "View on map";
        const string delete = "Delete track";
        string? choice = await Shell.Current.DisplayActionSheetAsync(Name, "Cancel", null, share, map, delete);
        switch (choice)
        {
            case share:
                await ShareAsync();
                break;
            case map:
                await ViewMapAsync();
                break;
            case delete:
                await DeleteAsync();
                break;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete track?",
            $"{Name} and its locally stored GPX file will be permanently deleted.",
            "Delete",
            "Cancel");
        if (!confirmed)
            return;

        await RunBusyAsync(() => trackService.DeleteTrackAsync(trackId));
        if (!HasError)
            await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static Task BackAsync() => Shell.Current.GoToAsync("..");
}
