using SQLite;

namespace Trailmark.Models;

[Table("Waypoints")]
public sealed class Waypoint
{
    [PrimaryKey, AutoIncrement]
    public long Id { get; set; }

    [Indexed]
    public long? TrackId { get; set; }

    [Indexed]
    public string SourceId { get; set; } = string.Empty;

    public int Sequence { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? ElevationMeters { get; set; }
    public DateTime? TimeUtc { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Symbol { get; set; }
}
