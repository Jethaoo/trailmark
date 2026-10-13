namespace Trailmark.Models;

public sealed class GpxDocument
{
    public List<ParsedTrack> Tracks { get; } = [];
    public List<ParsedWaypoint> Waypoints { get; } = [];
    public List<GpxParseIssue> Issues { get; } = [];
    public bool HasFatalError { get; internal set; }
}

public sealed class ParsedTrack
{
    public TrackKind Kind { get; init; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<ParsedSegment> Segments { get; } = [];
}

public sealed class ParsedSegment
{
    public List<ParsedPoint> Points { get; } = [];
}

public sealed record ParsedPoint(
    double Latitude,
    double Longitude,
    double? ElevationMeters,
    DateTime? TimeUtc,
    string? Name = null,
    string? Description = null);

public sealed record ParsedWaypoint(
    double Latitude,
    double Longitude,
    double? ElevationMeters,
    DateTime? TimeUtc,
    string? Name,
    string? Description,
    string? Symbol);

public sealed record GpxParseIssue(
    string Message,
    int? LineNumber = null,
    int? LinePosition = null,
    bool IsError = false);
