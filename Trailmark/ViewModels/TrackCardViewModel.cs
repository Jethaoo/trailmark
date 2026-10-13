using Trailmark.Helpers;
using Trailmark.Models;

namespace Trailmark.ViewModels;

public sealed record TrackCardViewModel(
    long Id,
    string Name,
    string Meta,
    string Distance,
    string Elevation,
    string RouteColor)
{
    public static TrackCardViewModel FromTrack(Track track, AppSettings? settings = null)
    {
        settings ??= AppSettings.Default;
        return new(
            track.Id,
            track.Name,
            BuildMeta(track),
            FormatDistance(track.DistanceMeters ?? 0d, settings.DistanceUnit),
            $"↗ {FormatElevation(track.ElevationGainMeters, settings.ElevationUnit)}",
            track.RouteColorHex);
    }

    private static string BuildMeta(Track track)
    {
        var date = track.ImportedAt.Date == DateTime.Today
            ? "Today"
            : track.ImportedAt.ToString("dd MMM");
        return track.Duration is { } duration
            ? $"{date} · {FormatDuration(duration)}"
            : date;
    }

    public static string FormatDistance(double meters, DistanceUnit unit = DistanceUnit.Kilometers) =>
        $"{UnitConverter.ConvertDistance(meters, unit):N1} {(unit == DistanceUnit.Miles ? "mi" : "km")}";

    public static string FormatElevation(double? meters, ElevationUnit unit)
    {
        if (meters is null)
            return "—";
        return $"{UnitConverter.ConvertElevation(meters.Value, unit):N0} {(unit == ElevationUnit.Feet ? "ft" : "m")}";
    }

    public static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
        : $"{Math.Max(1, duration.Minutes)}m";
}
