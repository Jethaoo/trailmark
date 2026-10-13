using SQLite;

namespace Trailmark.Models;

[Table("Settings")]
public sealed class Setting
{
    [PrimaryKey, MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum DistanceUnit
{
    Kilometers,
    Miles
}

public enum ElevationUnit
{
    Meters,
    Feet
}

public sealed record AppSettings(
    ThemePreference Theme,
    DistanceUnit DistanceUnit,
    ElevationUnit ElevationUnit,
    int TrackCount = 0,
    long StorageBytes = 0,
    string MapStyleId = "outdoor-v2")
{
    public static AppSettings Default { get; } = new(
        ThemePreference.System,
        DistanceUnit.Kilometers,
        ElevationUnit.Meters);
}
