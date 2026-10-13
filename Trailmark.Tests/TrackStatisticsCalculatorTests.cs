using Trailmark.Helpers;
using Trailmark.Models;
using Xunit;

namespace Trailmark.Tests;

public sealed class TrackStatisticsCalculatorTests
{
    private readonly TrackStatisticsCalculator _calculator = new();

    [Fact]
    public void Calculate_DoesNotConnectSeparateSegments()
    {
        var first = Segment(
            new ParsedPoint(0, 0, null, null),
            new ParsedPoint(0, 1, null, null));
        var second = Segment(
            new ParsedPoint(50, 50, null, null),
            new ParsedPoint(50, 51, null, null));
        var expected = TrackStatisticsCalculator.HaversineMeters(0, 0, 0, 1)
            + TrackStatisticsCalculator.HaversineMeters(50, 50, 50, 51);

        var result = _calculator.Calculate([first, second]);

        Assert.NotNull(result.DistanceMeters);
        Assert.Equal(expected, result.DistanceMeters.Value, 6);
        Assert.Equal(0, result.StartLatitude);
        Assert.Equal(51, result.EndLongitude);
    }

    [Fact]
    public void Calculate_ComputesElevationTimeAndSpeedFromAvailableValues()
    {
        var start = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var segment = Segment(
            new ParsedPoint(45, 7, 100, start),
            new ParsedPoint(45.001, 7.001, 130, start.AddMinutes(5)),
            new ParsedPoint(45.002, 7.002, 120, start.AddMinutes(10)));

        var result = _calculator.Calculate([segment]);

        Assert.Equal(30, result.ElevationGainMeters);
        Assert.Equal(10, result.ElevationLossMeters);
        Assert.Equal(100, result.MinimumElevationMeters);
        Assert.Equal(130, result.MaximumElevationMeters);
        Assert.Equal(TimeSpan.FromMinutes(10), result.Duration);
        Assert.Equal(result.DistanceMeters!.Value / 600, result.AverageSpeedMetersPerSecond);
        Assert.Equal(start, result.StartTimeUtc);
        Assert.Equal(start.AddMinutes(10), result.EndTimeUtc);
    }

    [Fact]
    public void Calculate_DoesNotInventMissingStatistics()
    {
        var result = _calculator.Calculate([Segment(new ParsedPoint(45, 7, null, null))]);

        Assert.NotNull(result.DistanceMeters);
        Assert.Equal(0d, result.DistanceMeters.Value);
        Assert.Null(result.ElevationGainMeters);
        Assert.Null(result.ElevationLossMeters);
        Assert.Null(result.MinimumElevationMeters);
        Assert.Null(result.MaximumElevationMeters);
        Assert.Null(result.Duration);
        Assert.Null(result.AverageSpeedMetersPerSecond);
        Assert.Null(result.StartTimeUtc);
        Assert.Null(result.EndTimeUtc);
    }

    private static ParsedSegment Segment(params ParsedPoint[] points)
    {
        var segment = new ParsedSegment();
        segment.Points.AddRange(points);
        return segment;
    }
}
