using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Trailmark.Models;
using Trailmark.Services;

namespace Trailmark.ViewModels;

public partial class TrackLibraryViewModel(ITrackService trackService, ISettingsService settingsService) : BaseViewModel
{
    private readonly List<Track> allTracks = [];
    private AppSettings settings = AppSettings.Default;

    public ObservableCollection<TrackCardViewModel> Tracks { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasTracks))]
    private int trackCount;

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSortRecent))]
    [NotifyPropertyChangedFor(nameof(IsSortLongest))]
    [NotifyPropertyChangedFor(nameof(IsSortHighestGain))]
    private string sortMode = "Recent";
    [ObservableProperty] private string sortDescription = "Newest first";

    public bool IsEmpty => !IsBusy && !HasError && TrackCount == 0;
    public bool HasTracks => TrackCount > 0;
    public string TrackCountLabel => $"{TrackCount} {(TrackCount == 1 ? "track" : "tracks")}";
    public bool IsSortRecent => SortMode == "Recent";
    public bool IsSortLongest => SortMode == "Longest";
    public bool IsSortHighestGain => SortMode == "Highest gain";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private Task LoadAsync() => RunBusyAsync(async () =>
    {
        allTracks.Clear();
        settings = await settingsService.GetAsync();
        allTracks.AddRange(await trackService.GetTracksAsync());
        ApplyFilter();
    });

    [RelayCommand]
    private void SetSort(string mode)
    {
        SortMode = mode;
        SortDescription = mode switch
        {
            "Longest" => "Longest first",
            "Highest gain" => "Highest gain first",
            _ => "Newest first"
        };
        ApplyFilter();
    }

    [RelayCommand]
    private static Task OpenTrackAsync(long id) => Shell.Current.GoToAsync($"track-details?trackId={id}");

    [RelayCommand]
    private static Task ImportAsync() => Shell.Current.GoToAsync("import");

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private async Task SortAsync()
    {
        if (Shell.Current is null)
            return;

        const string recent = "Recent";
        const string longest = "Longest";
        const string highestGain = "Highest gain";
        string? choice = await Shell.Current.DisplayActionSheetAsync("Sort tracks", "Cancel", null, recent, longest, highestGain);
        if (choice is null || choice == "Cancel")
            return;

        SetSort(choice);
    }

    [RelayCommand]
    private async Task MoreActionsAsync()
    {
        if (Shell.Current is null)
            return;

        const string import = "Import GPX";
        const string sort = "Sort tracks";
        const string refresh = "Refresh library";
        string? choice = await Shell.Current.DisplayActionSheetAsync("Track library", "Cancel", null, import, sort, refresh);
        switch (choice)
        {
            case import:
                await ImportAsync();
                break;
            case sort:
                await SortAsync();
                break;
            case refresh:
                await LoadAsync();
                break;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<Track> query = allTracks;
        if (!string.IsNullOrWhiteSpace(SearchText))
            query = query.Where(track => track.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase));

        query = SortMode switch
        {
            "Longest" => query.OrderByDescending(track => track.DistanceMeters),
            "Highest gain" => query.OrderByDescending(track => track.ElevationGainMeters),
            _ => query.OrderByDescending(track => track.ImportedAt)
        };

        Tracks.Clear();
        foreach (Track track in query)
            Tracks.Add(TrackCardViewModel.FromTrack(track, settings));

        TrackCount = Tracks.Count;
        OnPropertyChanged(nameof(TrackCountLabel));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasTracks));
    }
}
