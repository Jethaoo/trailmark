using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Trailmark.Models;
using Trailmark.Services;

namespace Trailmark.ViewModels;

public partial class ImportViewModel(IGpxImportService importService, ITrackService trackService) : BaseViewModel
{
    [ObservableProperty] private string state = "Idle";
    [ObservableProperty] private double progress;
    [ObservableProperty] private string importedTrackName = string.Empty;
    [ObservableProperty] private bool isIdle = true;
    [ObservableProperty] private bool isImporting;
    [ObservableProperty] private bool isSuccess;
    [ObservableProperty] private bool isFailed;
    private long importedTrackId;

    [RelayCommand]
    private async Task ChooseFileAsync()
    {
        SetState("Loading");
        await RunBusyAsync(async () =>
        {
            Progress = 0.2;
            ImportResult result = await importService.PickAndImportAsync();
            if (result.Status == ImportStatus.Cancelled)
            {
                SetState("Idle");
                return;
            }
            if (result.Status != ImportStatus.Success || result.TrackIds.Count == 0)
                throw new InvalidOperationException(result.ErrorMessage ?? "The GPX file could not be imported.");

            importedTrackId = result.TrackIds[0];
            Track? track = await trackService.GetTrackAsync(importedTrackId);
            ImportedTrackName = track?.Name ?? "Imported track";
            Progress = 1;
            SetState("Success");
        });

        if (HasError)
            SetState("Error");
    }

    [RelayCommand]
    private Task ViewTrackAsync() => Shell.Current.GoToAsync($"../track-details?trackId={importedTrackId}");

    [RelayCommand]
    private Task RetryAsync()
    {
        ErrorMessage = null;
        SetState("Idle");
        return Task.CompletedTask;
    }

    [RelayCommand]
    private static Task CloseAsync() => Shell.Current.GoToAsync("..");

    private void SetState(string value)
    {
        State = value;
        IsIdle = value == "Idle";
        IsImporting = value == "Loading";
        IsSuccess = value == "Success";
        IsFailed = value == "Error";
    }
}
