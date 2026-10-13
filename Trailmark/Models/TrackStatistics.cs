namespace Trailmark.Models;

public sealed record TrackStatistics(
    double? DistanceMeters,
    double? ElevationGainMeters,
    double? ElevationLossMeters,
    double? MinimumElevationMeters,
    double? MaximumElevationMeters,
    TimeSpan? Duration,
    double? AverageSpeedMetersPerSecond,
    double? StartLatitude,
    double? StartLongitude,
    double? EndLatitude,
    double? EndLongitude,
    DateTime? StartTimeUtc,
    DateTime? EndTimeUtc);
