using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trailmark.Helpers;
using Trailmark.Models;
using Trailmark.Services;

namespace Trailmark.ViewModels;

public partial class HomeViewModel(ITrackService trackService, ISettingsService settingsService) : BaseViewModel
{
    public ObservableCollection<TrackCardViewModel> RecentTracks { get; } = [];

    [ObservableProperty] private TrackCardViewModel? lastOpened;
    [ObservableProperty] private string greeting = "GOOD MORNING";
    [ObservableProperty] private string monthDistance = "0.0";
    [ObservableProperty] private string monthTrackCount = "0 tracks";
    [ObservableProperty] private string monthElevation = "0";
    [ObservableProperty] private string elevationTrend = "No change yet";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoLastOpened))]
    private bool hasLastOpened;

    public bool HasNoLastOpened => !HasLastOpened;

    [RelayCommand]
    private Task LoadAsync() => RunBusyAsync(async () =>
    {
        Greeting = DateTime.Now.Hour switch
        {
            < 12 => "GOOD MORNING",
            < 18 => "GOOD AFTERNOON",
            _ => "GOOD EVENING"
        };

        DashboardSummary summary = await trackService.GetDashboardAsync();
        AppSettings settings = await settingsService.GetAsync();
        MonthDistance = $"{UnitConverter.ConvertDistance(summary.MonthDistanceMeters, settings.DistanceUnit):N1} {(settings.DistanceUnit == DistanceUnit.Miles ? "mi" : "km")}";
        MonthTrackCount = $"{summary.MonthTrackCount} {(summary.MonthTrackCount == 1 ? "track" : "tracks")}";
        MonthElevation = TrackCardViewModel.FormatElevation(summary.MonthElevationGainMeters, settings.ElevationUnit);
        ElevationTrend = summary.MonthElevationChangePercent is { } change
            ? $"{(change >= 0 ? "↑" : "↓")} {Math.Abs(change):N0}% vs last month"
            : "No previous month";
        LastOpened = summary.LastOpened is null ? null : TrackCardViewModel.FromTrack(summary.LastOpened, settings);
        HasLastOpened = LastOpened is not null;

        RecentTracks.Clear();
        foreach (Track track in summary.RecentTracks.Take(2))
            RecentTracks.Add(TrackCardViewModel.FromTrack(track, settings));
    });

    [RelayCommand]
    private static Task OpenTrackAsync(long id) => Shell.Current.GoToAsync($"track-details?trackId={id}");

    [RelayCommand]
    private static Task OpenLastTrackAsync(TrackCardViewModel? track) => track is null
        ? Task.CompletedTask
        : Shell.Current.GoToAsync($"map?trackId={track.Id}");

    [RelayCommand]
    private static Task OpenLibraryAsync() => Shell.Current.GoToAsync("//library");

    [RelayCommand]
    private static Task OpenSettingsAsync() => Shell.Current.GoToAsync("//settings");

    [RelayCommand]
    private static Task ImportAsync() => Shell.Current.GoToAsync("import");

    [RelayCommand]
    private async Task TrackActionsAsync(long id)
    {
        if (Shell.Current is null)
            return;

        const string details = "Open details";
        const string map = "View on map";
        const string share = "Share GPX";
        const string delete = "Delete track";

        TrackCardViewModel? track = RecentTracks.FirstOrDefault(item => item.Id == id);
        string? choice = await Shell.Current.DisplayActionSheetAsync(track?.Name ?? "Track", "Cancel", null, details, map, share, delete);
        switch (choice)
        {
            case details:
                await OpenTrackAsync(id);
                break;
            case map:
                await Shell.Current.GoToAsync($"map?trackId={id}");
                break;
            case share:
                await RunBusyAsync(() => trackService.ShareAsync(id));
                break;
            case delete:
                bool confirmed = await Shell.Current.DisplayAlertAsync(
                    "Delete track?",
                    $"{track?.Name ?? "This track"} and its locally stored GPX file will be permanently deleted.",
                    "Delete",
                    "Cancel");
                if (!confirmed)
                    return;

                await RunBusyAsync(() => trackService.DeleteTrackAsync(id));
                if (!HasError)
                    await LoadAsync();
                break;
        }
    }
}
