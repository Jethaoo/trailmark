using SQLite;

namespace Trailmark.Models;

[Table("TrackPoints")]
public sealed class TrackPoint
{
    [PrimaryKey, AutoIncrement]
    public long Id { get; set; }

    [Indexed("IX_TrackPoints_TrackSequence", 1, Unique = true)]
    [Indexed("IX_TrackPoints_TrackSegment", 1)]
    public long TrackId { get; set; }

    [Indexed("IX_TrackPoints_TrackSequence", 2, Unique = true)]
    public int Sequence { get; set; }

    [Indexed("IX_TrackPoints_TrackSegment", 2)]
    public int SegmentIndex { get; set; }

    public int SegmentSequence { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? ElevationMeters { get; set; }
    public DateTime? TimeUtc { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
