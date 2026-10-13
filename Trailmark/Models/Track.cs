using SQLite;

namespace Trailmark.Models;

public enum TrackKind
{
    Track = 0,
    Route = 1,
    WaypointCollection = 2
}

[Table("Tracks")]
public sealed class Track
{
    [PrimaryKey, AutoIncrement]
    public long Id { get; set; }

    [Indexed]
    public string SourceId { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public TrackKind Kind { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalFilePath { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; }
    public int PointCount { get; set; }
    public int SegmentCount { get; set; }
    public double? DistanceMeters { get; set; }
    public double? ElevationGainMeters { get; set; }
    public double? ElevationLossMeters { get; set; }
    public double? MinimumElevationMeters { get; set; }
    public double? MaximumElevationMeters { get; set; }
    public double? DurationSeconds { get; set; }
    public double? AverageSpeedMetersPerSecond { get; set; }
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public double? EndLatitude { get; set; }
    public double? EndLongitude { get; set; }
    public DateTime? StartTimeUtc { get; set; }
    public DateTime? EndTimeUtc { get; set; }

    [Ignore]
    public string ActivityType => Kind switch
    {
        TrackKind.Route => "Route",
        TrackKind.WaypointCollection => "Waypoints",
        _ => "Hiking"
    };

    [Ignore]
    public string? Location => null;

    [Ignore]
    public DateTime ImportedAt => ImportedAtUtc.ToLocalTime();

    [Ignore]
    public TimeSpan? Duration => DurationSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null;

    [Ignore]
    public double DistanceMetersValue => DistanceMeters ?? 0d;

    [Ignore]
    public double ElevationGainMetersValue => ElevationGainMeters ?? 0d;

    [Ignore]
    public double ElevationLossMetersValue => ElevationLossMeters ?? 0d;

    [Ignore]
    public double? MinElevationMeters => MinimumElevationMeters;

    [Ignore]
    public double? MaxElevationMeters => MaximumElevationMeters;

    [Ignore]
    public int WaypointCount { get; set; }

    [Ignore]
    public string RouteColorHex => Kind == TrackKind.Route ? "#43849C" : "#658F35";

    [Ignore]
    public IReadOnlyList<ElevationSample> ElevationProfile { get; set; } = [];
}

public sealed record ElevationSample(double DistanceMeters, double ElevationMeters);

public sealed record DashboardSummary(
    double MonthDistanceMeters,
    int MonthTrackCount,
    double MonthElevationGainMeters,
    double? MonthElevationChangePercent,
    Track? LastOpened,
    IReadOnlyList<Track> RecentTracks);
