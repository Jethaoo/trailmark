using Trailmark.Models;

namespace Trailmark.Helpers;

public interface ITrackStatisticsCalculator
{
    TrackStatistics Calculate(IEnumerable<ParsedSegment> segments);
}

public sealed class TrackStatisticsCalculator : ITrackStatisticsCalculator
{
    private const double EarthRadiusMeters = 6_371_008.8;

    public TrackStatistics Calculate(IEnumerable<ParsedSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var distance = 0d;
        var gain = 0d;
        var loss = 0d;
        var elevationPairCount = 0;
        double? minimumElevation = null;
        double? maximumElevation = null;
        ParsedPoint? firstPoint = null;
        ParsedPoint? lastPoint = null;
        DateTime? firstTime = null;
        DateTime? lastTime = null;
        var timestampCount = 0;

        foreach (var segment in segments)
        {
            ParsedPoint? previous = null;
            foreach (var point in segment.Points)
            {
                firstPoint ??= point;
                lastPoint = point;

                if (point.TimeUtc is { } time)
                {
                    firstTime ??= time;
                    lastTime = time;
                    timestampCount++;
                }

                if (point.ElevationMeters is { } elevation)
                {
                    minimumElevation = minimumElevation is null ? elevation : Math.Min(minimumElevation.Value, elevation);
                    maximumElevation = maximumElevation is null ? elevation : Math.Max(maximumElevation.Value, elevation);
                }

                if (previous is not null)
                {
                    distance += HaversineMeters(
                        previous.Latitude,
                        previous.Longitude,
                        point.Latitude,
                        point.Longitude);

                    if (previous.ElevationMeters is { } previousElevation && point.ElevationMeters is { } currentElevation)
                    {
                        var difference = currentElevation - previousElevation;
                        if (difference > 0)
                            gain += difference;
                        else
                            loss += -difference;
                        elevationPairCount++;
                    }
                }

                previous = point;
            }
        }

        double? distanceValue = firstPoint is null ? null : distance;
        TimeSpan? duration = null;
        double? averageSpeed = null;
        if (timestampCount >= 2 && firstTime is { } start && lastTime is { } end && end >= start)
        {
            duration = end - start;
            if (duration.Value.TotalSeconds > 0 && distanceValue is { } measuredDistance)
                averageSpeed = measuredDistance / duration.Value.TotalSeconds;
        }

        return new TrackStatistics(
            distanceValue,
            elevationPairCount > 0 ? gain : null,
            elevationPairCount > 0 ? loss : null,
            minimumElevation,
            maximumElevation,
            duration,
            averageSpeed,
            firstPoint?.Latitude,
            firstPoint?.Longitude,
            lastPoint?.Latitude,
            lastPoint?.Longitude,
            firstTime,
            lastTime);
    }

    public static double HaversineMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var latitudeDelta = DegreesToRadians(latitude2 - latitude1);
        var longitudeDelta = DegreesToRadians(longitude2 - longitude1);
        var latitude1Radians = DegreesToRadians(latitude1);
        var latitude2Radians = DegreesToRadians(latitude2);

        var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(latitude1Radians) * Math.Cos(latitude2Radians)
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        a = Math.Clamp(a, 0, 1);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
