using Microsoft.Maui.Storage;

namespace Trailmark.Services;

public sealed record PickedFile(string FileName, Func<Task<Stream>> OpenReadAsync);

public interface IFilePickerService
{
    Task<PickedFile?> PickGpxFileAsync(CancellationToken cancellationToken = default);
}

public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<PickedFile?> PickGpxFileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a GPX file"
        }).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return result is null
            ? null
            : new PickedFile(result.FileName, result.OpenReadAsync);
    }
}
