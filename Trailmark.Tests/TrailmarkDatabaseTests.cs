using Trailmark.Data;
using Trailmark.Models;
using Xunit;

namespace Trailmark.Tests;

public sealed class TrailmarkDatabaseTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"trailmark-{Guid.NewGuid():N}.db3");
    private TrailmarkDatabase _database = null!;

    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        _database = new TrailmarkDatabase(_databasePath);
        await _database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task InsertAndQuery_PreservesPointAndSegmentSequence()
    {
        var track = CreateTrack("Sequence test", "source-a");
        var points = new[]
        {
            new TrackPoint { Sequence = 0, SegmentIndex = 0, SegmentSequence = 0, Latitude = 1, Longitude = 1 },
            new TrackPoint { Sequence = 1, SegmentIndex = 1, SegmentSequence = 0, Latitude = 2, Longitude = 2 },
            new TrackPoint { Sequence = 2, SegmentIndex = 1, SegmentSequence = 1, Latitude = 3, Longitude = 3 }
        };

        var id = await _database.InsertTrackAsync(track, points, Array.Empty<Waypoint>());
        var stored = await _database.GetTrackPointsAsync(id);

        Assert.Equal(new[] { 0, 1, 2 }, stored.Select(point => point.Sequence));
        Assert.Equal(new[] { 0, 1, 1 }, stored.Select(point => point.SegmentIndex));
        Assert.All(stored, point => Assert.Equal(id, point.TrackId));
    }

    [Fact]
    public async Task SearchAndDelete_HandlesRelatedRows()
    {
        var matching = CreateTrack("Alpine Loop", "source-b");
        var other = CreateTrack("Coastal Walk", "source-c");
        var matchingId = await _database.InsertTrackAsync(
            matching,
            [new TrackPoint { Sequence = 0, Latitude = 1, Longitude = 1 }],
            [new Waypoint { SourceId = "source-b", Sequence = 0, Latitude = 1, Longitude = 1 }]);
        await _database.InsertTrackAsync(other, Array.Empty<TrackPoint>(), Array.Empty<Waypoint>());

        var results = await _database.SearchTracksAsync(new TrackSearchOptions("alpine", TrackSort.NameAscending));
        var deleted = await _database.DeleteTrackAsync(matchingId);

        Assert.Single(results);
        Assert.Equal("Alpine Loop", results[0].Name);
        Assert.True(deleted);
        Assert.Empty(await _database.GetTrackPointsAsync(matchingId));
        Assert.Empty(await _database.GetWaypointsAsync(matchingId));
    }

    private static Track CreateTrack(string name, string sourceId) => new()
    {
        Name = name,
        SourceId = sourceId,
        OriginalFileName = "test.gpx",
        OriginalFilePath = "test.gpx",
        ImportedAtUtc = DateTime.UtcNow
    };
}
