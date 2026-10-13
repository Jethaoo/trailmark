namespace Trailmark.Models;

public enum LocationStatus
{
    Success,
    PermissionDenied,
    Disabled,
    Unavailable,
    Error
}

public sealed record LocationResult(
    LocationStatus Status,
    double? Latitude = null,
    double? Longitude = null,
    double? AccuracyMeters = null,
    DateTime? TimestampUtc = null,
    string? ErrorMessage = null)
{
    public bool IsSuccess => Status == LocationStatus.Success;
}
