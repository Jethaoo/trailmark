using Microsoft.Maui.ApplicationModel.DataTransfer;
using Trailmark.Data;

namespace Trailmark.Services;

public interface IShareService
{
    Task ShareTrackAsync(long trackId, CancellationToken cancellationToken = default);
    Task ExportTrackAsync(long trackId, Stream destination, CancellationToken cancellationToken = default);
}

public sealed class ShareService(ITrailmarkDatabase database) : IShareService
{
    public async Task ShareTrackAsync(long trackId, CancellationToken cancellationToken = default)
    {
        var track = await database.GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Track {trackId} was not found.");
        if (!File.Exists(track.OriginalFilePath))
            throw new FileNotFoundException("The original GPX file is no longer available.", track.OriginalFilePath);

        cancellationToken.ThrowIfCancellationRequested();
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = $"Share {track.Name}",
            File = new ShareFile(track.OriginalFilePath)
        }).ConfigureAwait(false);
    }

    public async Task ExportTrackAsync(long trackId, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("The export stream must be writable.", nameof(destination));

        var track = await database.GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Track {trackId} was not found.");
        await using var source = new FileStream(
            track.OriginalFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, 81_920, cancellationToken).ConfigureAwait(false);
    }
}
