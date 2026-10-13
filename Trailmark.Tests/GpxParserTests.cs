using System.Text;
using Trailmark.Models;
using Trailmark.Services;
using Xunit;

namespace Trailmark.Tests;

public sealed class GpxParserTests
{
    private readonly GpxParser _parser = new();

    [Fact]
    public async Task ParseAsync_ParsesNamespacesTracksRoutesSegmentsAndWaypoints()
    {
        const string xml = """
            <?xml version="1.0"?>
            <gpx xmlns="http://www.topografix.com/GPX/1/1" version="1.1">
              <wpt lat="51.5" lon="-0.1"><name>Lookout</name><desc>View</desc><sym>Flag</sym></wpt>
              <trk>
                <name>Morning walk</name><desc>Two segments</desc>
                <trkseg>
                  <trkpt lat="51.5000" lon="-0.1000"><ele>12.5</ele><time>2025-01-01T10:00:00Z</time></trkpt>
                  <trkpt lat="51.5005" lon="-0.1005" />
                </trkseg>
                <trkseg><trkpt lat="51.6" lon="-0.2" /></trkseg>
              </trk>
              <rte><name>Planned route</name><rtept lat="52" lon="1"><name>Turn</name></rtept></rte>
            </gpx>
            """;

        var result = await _parser.ParseAsync(ToStream(xml));

        Assert.False(result.HasFatalError);
        Assert.Equal(2, result.Tracks.Count);
        Assert.Equal(TrackKind.Track, result.Tracks[0].Kind);
        Assert.Equal(2, result.Tracks[0].Segments.Count);
        Assert.Equal(3, result.Tracks[0].Segments.Sum(segment => segment.Points.Count));
        Assert.Equal(12.5, result.Tracks[0].Segments[0].Points[0].ElevationMeters);
        Assert.Equal(TrackKind.Route, result.Tracks[1].Kind);
        Assert.Single(result.Tracks[1].Segments[0].Points);
        var waypoint = Assert.Single(result.Waypoints);
        Assert.Equal("Lookout", waypoint.Name);
        Assert.Equal("Flag", waypoint.Symbol);
    }

    [Fact]
    public async Task ParseAsync_SkipsInvalidPointsAndReportsIssues()
    {
        const string xml = """
            <gpx><trk><trkseg>
              <trkpt lat="not-a-number" lon="2" />
              <trkpt lat="91" lon="2" />
              <trkpt lat="45" lon="2"><ele>bad</ele><time>bad</time></trkpt>
            </trkseg></trk></gpx>
            """;

        var result = await _parser.ParseAsync(ToStream(xml));

        Assert.False(result.HasFatalError);
        Assert.Single(result.Tracks[0].Segments[0].Points);
        Assert.Null(result.Tracks[0].Segments[0].Points[0].ElevationMeters);
        Assert.Null(result.Tracks[0].Segments[0].Points[0].TimeUtc);
        Assert.Equal(4, result.Issues.Count);
    }

    [Fact]
    public async Task ParseAsync_ReturnsGracefulErrorForMalformedXml()
    {
        var result = await _parser.ParseAsync(ToStream("<gpx><trk><trkseg></gpx>"));

        Assert.True(result.HasFatalError);
        Assert.Contains(result.Issues, issue => issue.IsError && issue.Message.Contains("Malformed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParseAsync_HandlesFiftyThousandPoints()
    {
        var builder = new StringBuilder("<gpx><trk><trkseg>", 2_500_000);
        for (var index = 0; index < 50_000; index++)
            builder.Append("<trkpt lat=\"45.0\" lon=\"7.0\"/>");
        builder.Append("</trkseg></trk></gpx>");

        var result = await _parser.ParseAsync(ToStream(builder.ToString()));

        Assert.False(result.HasFatalError);
        Assert.Equal(50_000, result.Tracks[0].Segments[0].Points.Count);
    }

    private static MemoryStream ToStream(string value) => new(Encoding.UTF8.GetBytes(value));
}
