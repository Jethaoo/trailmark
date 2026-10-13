using Trailmark.Data;
using Trailmark.Helpers;
using Trailmark.Models;

namespace Trailmark.Services;

public interface IGpxImportService
{
    Task<ImportResult> PickAndImportAsync(CancellationToken cancellationToken = default);
    Task<ImportResult> ImportAsync(PickedFile file, CancellationToken cancellationToken = default);
    Task<bool> DeleteImportedTrackAsync(long trackId, CancellationToken cancellationToken = default);
}

public sealed class GpxImportService(
    IFilePickerService filePicker,
    ILocalFileStorage fileStorage,
    IGpxParser parser,
    ITrackStatisticsCalculator statisticsCalculator,
    ITrailmarkDatabase database) : IGpxImportService
{
    public async Task<ImportResult> PickAndImportAsync(CancellationToken cancellationToken = default)
    {
        var file = await filePicker.PickGpxFileAsync(cancellationToken).ConfigureAwait(false);
        return file is null
            ? ImportResult.Cancelled
            : await ImportAsync(file, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportResult> ImportAsync(PickedFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!string.Equals(Path.GetExtension(file.FileName), ".gpx", StringComparison.OrdinalIgnoreCase))
        {
            return new ImportResult(
                ImportStatus.InvalidFile,
                Array.Empty<long>(),
                Array.Empty<GpxParseIssue>(),
                "Select a file with the .gpx extension.");
        }

        var sourceId = Guid.NewGuid().ToString("N");
        var storedPath = string.Empty;
        var insertedTrackIds = new List<long>();
        try
        {
            storedPath = await fileStorage.SaveOriginalAsync(file, sourceId, cancellationToken).ConfigureAwait(false);
            GpxDocument document;
            await using (var stream = await fileStorage.OpenReadAsync(storedPath, cancellationToken).ConfigureAwait(false))
            {
                document = await parser.ParseAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            if (document.HasFatalError)
            {
                await fileStorage.DeleteIfExistsAsync(storedPath, CancellationToken.None).ConfigureAwait(false);
                return new ImportResult(
                    ImportStatus.InvalidFile,
                    Array.Empty<long>(),
                    document.Issues,
                    "The GPX file is malformed and was not imported.");
            }

            if (document.Tracks.Count == 0 && document.Waypoints.Count == 0)
            {
                await fileStorage.DeleteIfExistsAsync(storedPath, CancellationToken.None).ConfigureAwait(false);
                return new ImportResult(
                    ImportStatus.InvalidFile,
                    Array.Empty<long>(),
                    document.Issues,
                    "The GPX file contains no tracks, routes, or waypoints.");
            }

            if (document.Tracks.Count == 0)
            {
                document.Tracks.Add(new ParsedTrack
                {
                    Kind = TrackKind.WaypointCollection,
                    Name = Path.GetFileNameWithoutExtension(file.FileName)
                });
            }

            var importedAt = DateTime.UtcNow;
            var sharedWaypoints = CreateWaypoints(document.Waypoints, sourceId);
            for (var index = 0; index < document.Tracks.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parsedTrack = document.Tracks[index];
                var statistics = statisticsCalculator.Calculate(parsedTrack.Segments);
                var points = CreateTrackPoints(parsedTrack.Segments);
                var fallbackName = CreateFallbackName(file.FileName, parsedTrack.Kind, index, document.Tracks.Count);
                var track = CreateTrack(
                    parsedTrack,
                    statistics,
                    points.Count,
                    sourceId,
                    file.FileName,
                    storedPath,
                    importedAt,
                    fallbackName);

                var trackId = await database.InsertTrackAsync(
                    track,
                    points,
                    index == 0 ? sharedWaypoints : Array.Empty<Waypoint>(),
                    cancellationToken).ConfigureAwait(false);
                insertedTrackIds.Add(trackId);
            }

            return new ImportResult(ImportStatus.Success, insertedTrackIds, document.Issues);
        }
        catch (OperationCanceledException)
        {
            await RollBackAsync(insertedTrackIds, storedPath).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await RollBackAsync(insertedTrackIds, storedPath).ConfigureAwait(false);
            return new ImportResult(
                ImportStatus.Failed,
                Array.Empty<long>(),
                Array.Empty<GpxParseIssue>(),
                $"The GPX file could not be imported: {exception.Message}");
        }
    }

    public async Task<bool> DeleteImportedTrackAsync(long trackId, CancellationToken cancellationToken = default)
    {
        var track = await database.GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
        if (track is null)
            return false;

        var deleted = await database.DeleteTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
        if (deleted && await database.CountTracksBySourceAsync(track.SourceId, cancellationToken).ConfigureAwait(false) == 0)
            await fileStorage.DeleteIfExistsAsync(track.OriginalFilePath, cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    private static Track CreateTrack(
        ParsedTrack parsedTrack,
        TrackStatistics statistics,
        int pointCount,
        string sourceId,
        string originalFileName,
        string storedPath,
        DateTime importedAt,
        string fallbackName) => new()
    {
        SourceId = sourceId,
        Name = string.IsNullOrWhiteSpace(parsedTrack.Name) ? fallbackName : parsedTrack.Name,
        Description = parsedTrack.Description,
        Kind = parsedTrack.Kind,
        OriginalFileName = Path.GetFileName(originalFileName),
        OriginalFilePath = storedPath,
        ImportedAtUtc = importedAt,
        PointCount = pointCount,
        SegmentCount = parsedTrack.Segments.Count,
        DistanceMeters = statistics.DistanceMeters,
        ElevationGainMeters = statistics.ElevationGainMeters,
        ElevationLossMeters = statistics.ElevationLossMeters,
        MinimumElevationMeters = statistics.MinimumElevationMeters,
        MaximumElevationMeters = statistics.MaximumElevationMeters,
        DurationSeconds = statistics.Duration?.TotalSeconds,
        AverageSpeedMetersPerSecond = statistics.AverageSpeedMetersPerSecond,
        StartLatitude = statistics.StartLatitude,
        StartLongitude = statistics.StartLongitude,
        EndLatitude = statistics.EndLatitude,
        EndLongitude = statistics.EndLongitude,
        StartTimeUtc = statistics.StartTimeUtc,
        EndTimeUtc = statistics.EndTimeUtc
    };

    private static List<TrackPoint> CreateTrackPoints(IEnumerable<ParsedSegment> segments)
    {
        var points = new List<TrackPoint>();
        var sequence = 0;
        var segmentIndex = 0;
        foreach (var segment in segments)
        {
            var segmentSequence = 0;
            foreach (var point in segment.Points)
            {
                points.Add(new TrackPoint
                {
                    Sequence = sequence++,
                    SegmentIndex = segmentIndex,
                    SegmentSequence = segmentSequence++,
                    Latitude = point.Latitude,
                    Longitude = point.Longitude,
                    ElevationMeters = point.ElevationMeters,
                    TimeUtc = point.TimeUtc,
                    Name = point.Name,
                    Description = point.Description
                });
            }
            segmentIndex++;
        }
        return points;
    }

    private static List<Waypoint> CreateWaypoints(IEnumerable<ParsedWaypoint> parsedWaypoints, string sourceId)
    {
        var sequence = 0;
        return parsedWaypoints.Select(waypoint => new Waypoint
        {
            SourceId = sourceId,
            Sequence = sequence++,
            Latitude = waypoint.Latitude,
            Longitude = waypoint.Longitude,
            ElevationMeters = waypoint.ElevationMeters,
            TimeUtc = waypoint.TimeUtc,
            Name = waypoint.Name,
            Description = waypoint.Description,
            Symbol = waypoint.Symbol
        }).ToList();
    }

    private static string CreateFallbackName(string fileName, TrackKind kind, int index, int totalTracks)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (totalTracks == 1)
            return baseName;
        var label = kind == TrackKind.Route ? "Route" : "Track";
        return $"{baseName} — {label} {index + 1}";
    }

    private async Task RollBackAsync(IEnumerable<long> trackIds, string storedPath)
    {
        foreach (var trackId in trackIds)
        {
            try
            {
                await database.DeleteTrackAsync(trackId, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Preserve the original failure; a later cleanup can remove an orphaned import.
            }
        }

        try
        {
            await fileStorage.DeleteIfExistsAsync(storedPath, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Preserve the original failure; app-private orphan files are safe to clean up later.
        }
    }
}
