using Trailmark.Data;
using Trailmark.Helpers;
using Trailmark.Models;

namespace Trailmark.Services;

public interface ITrackService
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Track>> GetTracksAsync(CancellationToken cancellationToken = default);
    Task<Track?> GetTrackAsync(long id, CancellationToken cancellationToken = default);
    Task ShareAsync(long id, CancellationToken cancellationToken = default);
    Task DeleteTrackAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class TrackService(
    ITrailmarkDatabase database,
    IShareService shareService,
    IGpxImportService importService) : ITrackService
{
    public async Task<IReadOnlyList<Track>> GetTracksAsync(CancellationToken cancellationToken = default) =>
        await database.SearchTracksAsync(new TrackSearchOptions(Take: 500), cancellationToken).ConfigureAwait(false);

    public async Task<Track?> GetTrackAsync(long id, CancellationToken cancellationToken = default)
    {
        var track = await database.GetTrackAsync(id, cancellationToken).ConfigureAwait(false);
        if (track is null)
            return null;

        var points = await database.GetTrackPointsAsync(id, cancellationToken).ConfigureAwait(false);
        var waypoints = await database.GetWaypointsAsync(id, cancellationToken).ConfigureAwait(false);
        track.WaypointCount = waypoints.Count;
        track.ElevationProfile = CreateElevationProfile(points);
        return track;
    }

    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tracks = await GetTracksAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousStart = monthStart.AddMonths(-1);
        var current = tracks.Where(track => track.ImportedAtUtc >= monthStart).ToArray();
        var previousGain = tracks.Where(track => track.ImportedAtUtc >= previousStart && track.ImportedAtUtc < monthStart)
            .Sum(track => track.ElevationGainMeters ?? 0d);
        var currentGain = current.Sum(track => track.ElevationGainMeters ?? 0d);
        double? change = previousGain > 0d ? (currentGain - previousGain) / previousGain * 100d : null;

        return new DashboardSummary(
            current.Sum(track => track.DistanceMeters ?? 0d),
            current.Length,
            currentGain,
            change,
            tracks.FirstOrDefault(),
            tracks.Take(5).ToArray());
    }

    public Task ShareAsync(long id, CancellationToken cancellationToken = default) =>
        shareService.ShareTrackAsync(id, cancellationToken);

    public async Task DeleteTrackAsync(long id, CancellationToken cancellationToken = default) =>
        _ = await importService.DeleteImportedTrackAsync(id, cancellationToken).ConfigureAwait(false);

    private static IReadOnlyList<ElevationSample> CreateElevationProfile(IReadOnlyList<TrackPoint> points)
    {
        var samples = new List<ElevationSample>();
        var distance = 0d;
        TrackPoint? previous = null;
        foreach (var point in points)
        {
            if (previous is not null && previous.SegmentIndex == point.SegmentIndex)
                distance += TrackStatisticsCalculator.HaversineMeters(
                    previous.Latitude, previous.Longitude, point.Latitude, point.Longitude);
            if (point.ElevationMeters is { } elevation)
                samples.Add(new ElevationSample(distance, elevation));
            previous = point;
        }

        if (samples.Count <= 400)
            return samples;
        var step = (double)(samples.Count - 1) / 399d;
        return Enumerable.Range(0, 400).Select(index => samples[(int)Math.Round(index * step)]).ToArray();
    }
}

public interface IMapService
{
    Task<string> CreateTrackMapHtmlAsync(long trackId, string? styleId = null, CancellationToken cancellationToken = default);
}

public sealed class MapService(IMapTilerService mapTilerService) : IMapService
{
    public Task<string> CreateTrackMapHtmlAsync(long trackId, string? styleId = null, CancellationToken cancellationToken = default) =>
        mapTilerService.CreateTrackMapHtmlAsync(trackId, styleId, null, cancellationToken);
}
