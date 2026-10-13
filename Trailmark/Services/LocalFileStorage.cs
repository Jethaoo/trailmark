using Microsoft.Maui.Storage;

namespace Trailmark.Services;

public interface ILocalFileStorage
{
    Task<string> SaveOriginalAsync(PickedFile file, string sourceId, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteIfExistsAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class LocalFileStorage : ILocalFileStorage
{
    private const string ImportDirectoryName = "Imports";

    public async Task<string> SaveOriginalAsync(
        PickedFile file,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        var directory = Path.Combine(FileSystem.AppDataDirectory, ImportDirectoryName);
        Directory.CreateDirectory(directory);
        var destinationPath = Path.Combine(directory, $"{sourceId}.gpx");

        try
        {
            await using var source = await file.OpenReadAsync().ConfigureAwait(false);
            await using var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, 81_920, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            return destinationPath;
        }
        catch
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteIfExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }
}
