using SQLite;
using Trailmark.Models;

namespace Trailmark.Data;

public enum TrackSort
{
    ImportedNewest,
    ImportedOldest,
    NameAscending,
    NameDescending,
    DistanceDescending,
    DurationDescending
}

public sealed record TrackSearchOptions(
    string? SearchText = null,
    TrackSort Sort = TrackSort.ImportedNewest,
    int Skip = 0,
    int Take = 100);

public interface ITrailmarkDatabase
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<long> InsertTrackAsync(
        Track track,
        IReadOnlyCollection<TrackPoint> points,
        IReadOnlyCollection<Waypoint> waypoints,
        CancellationToken cancellationToken = default);
    Task<Track?> GetTrackAsync(long trackId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Track>> SearchTracksAsync(TrackSearchOptions options, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackPoint>> GetTrackPointsAsync(long trackId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Waypoint>> GetWaypointsAsync(long trackId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTrackAsync(long trackId, CancellationToken cancellationToken = default);
    Task<int> CountTracksBySourceAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<(int TrackCount, IReadOnlyList<string> FilePaths)> GetStorageSummaryAsync(CancellationToken cancellationToken = default);
    Task<Setting?> GetSettingAsync(string key, CancellationToken cancellationToken = default);
    Task SetSettingAsync(Setting setting, CancellationToken cancellationToken = default);
}

public sealed class TrailmarkDatabase : ITrailmarkDatabase, IAsyncDisposable
{
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public TrailmarkDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            _ = await _connection.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL;").ConfigureAwait(false);
            await _connection.ExecuteAsync("PRAGMA synchronous=NORMAL;").ConfigureAwait(false);
            await _connection.ExecuteAsync("PRAGMA foreign_keys=ON;").ConfigureAwait(false);
            await _connection.CreateTablesAsync<Track, TrackPoint, Waypoint, Setting>().ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<long> InsertTrackAsync(
        Track track,
        IReadOnlyCollection<TrackPoint> points,
        IReadOnlyCollection<Waypoint> waypoints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(waypoints);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        await _connection.RunInTransactionAsync(connection =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            connection.Insert(track);

            foreach (var point in points)
                point.TrackId = track.Id;
            foreach (var waypoint in waypoints.Where(item => item.TrackId is null && string.IsNullOrEmpty(item.SourceId)))
                waypoint.TrackId = track.Id;

            if (points.Count > 0)
                connection.InsertAll(points, false);
            if (waypoints.Count > 0)
                connection.InsertAll(waypoints, false);
        }).ConfigureAwait(false);

        return track.Id;
    }

    public async Task<Track?> GetTrackAsync(long trackId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await _connection.FindAsync<Track>(trackId).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Track>> SearchTracksAsync(
        TrackSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Skip < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Skip cannot be negative.");
        if (options.Take is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(options), "Take must be between 1 and 500.");

        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var orderBy = options.Sort switch
        {
            TrackSort.ImportedOldest => "ImportedAtUtc ASC",
            TrackSort.NameAscending => "Name COLLATE NOCASE ASC",
            TrackSort.NameDescending => "Name COLLATE NOCASE DESC",
            TrackSort.DistanceDescending => "DistanceMeters DESC",
            TrackSort.DurationDescending => "DurationSeconds DESC",
            _ => "ImportedAtUtc DESC"
        };

        var parameters = new List<object>();
        var sql = "SELECT * FROM Tracks";
        if (!string.IsNullOrWhiteSpace(options.SearchText))
        {
            sql += " WHERE Name LIKE ? ESCAPE '\\' COLLATE NOCASE OR Description LIKE ? ESCAPE '\\' COLLATE NOCASE";
            var escaped = EscapeLike(options.SearchText.Trim());
            parameters.Add($"%{escaped}%");
            parameters.Add($"%{escaped}%");
        }

        sql += $" ORDER BY {orderBy}, Id DESC LIMIT ? OFFSET ?";
        parameters.Add(options.Take);
        parameters.Add(options.Skip);
        return await _connection.QueryAsync<Track>(sql, parameters.ToArray()).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TrackPoint>> GetTrackPointsAsync(
        long trackId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await _connection.QueryAsync<TrackPoint>(
            "SELECT * FROM TrackPoints WHERE TrackId = ? ORDER BY Sequence ASC",
            trackId).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Waypoint>> GetWaypointsAsync(
        long trackId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var track = await _connection.FindAsync<Track>(trackId).ConfigureAwait(false);
        if (track is null)
            return [];

        return await _connection.QueryAsync<Waypoint>(
            "SELECT * FROM Waypoints WHERE TrackId = ? OR (SourceId = ? AND TrackId IS NULL) ORDER BY Sequence ASC",
            trackId,
            track.SourceId).ConfigureAwait(false);
    }

    public async Task<bool> DeleteTrackAsync(long trackId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var deleted = false;

        await _connection.RunInTransactionAsync(connection =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var track = connection.Find<Track>(trackId);
            if (track is null)
                return;

            connection.Execute("DELETE FROM TrackPoints WHERE TrackId = ?", trackId);
            connection.Execute("DELETE FROM Waypoints WHERE TrackId = ?", trackId);
            connection.Delete(track);

            var remaining = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM Tracks WHERE SourceId = ?",
                track.SourceId);
            if (remaining == 0)
                connection.Execute("DELETE FROM Waypoints WHERE SourceId = ?", track.SourceId);
            deleted = true;
        }).ConfigureAwait(false);

        return deleted;
    }

    public async Task<int> CountTracksBySourceAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Tracks WHERE SourceId = ?",
            sourceId).ConfigureAwait(false);
    }

    public async Task<(int TrackCount, IReadOnlyList<string> FilePaths)> GetStorageSummaryAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var count = await _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Tracks").ConfigureAwait(false);
        var paths = await _connection.QueryScalarsAsync<string>(
            "SELECT DISTINCT OriginalFilePath FROM Tracks WHERE OriginalFilePath <> ''").ConfigureAwait(false);
        return (count, paths);
    }

    public async Task<Setting?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await _connection.FindAsync<Setting>(key).ConfigureAwait(false);
    }

    public async Task SetSettingAsync(Setting setting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentException.ThrowIfNullOrWhiteSpace(setting.Key);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        setting.UpdatedAtUtc = DateTime.UtcNow;
        await _connection.InsertOrReplaceAsync(setting).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            _initialized = false;
        }
        _initializationLock.Dispose();
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);
}
