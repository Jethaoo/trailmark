using Trailmark.Data;
using Trailmark.Models;

namespace Trailmark.Services;

public interface ISettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task SetDistanceUnitAsync(DistanceUnit unit, CancellationToken cancellationToken = default);
    Task SetElevationUnitAsync(ElevationUnit unit, CancellationToken cancellationToken = default);
    Task SetThemeAsync(ThemePreference theme, CancellationToken cancellationToken = default);
    Task SetMapStyleAsync(string styleId, CancellationToken cancellationToken = default);
}

public sealed class SettingsService(ITrailmarkDatabase database) : ISettingsService
{
    private const string ThemeKey = "theme";
    private const string DistanceUnitKey = "distance_unit";
    private const string ElevationUnitKey = "elevation_unit";
    private const string MapStyleKey = "map_style";

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var theme = await GetEnumAsync(ThemeKey, AppSettings.Default.Theme, cancellationToken).ConfigureAwait(false);
        var distance = await GetEnumAsync(DistanceUnitKey, AppSettings.Default.DistanceUnit, cancellationToken).ConfigureAwait(false);
        var elevation = await GetEnumAsync(ElevationUnitKey, AppSettings.Default.ElevationUnit, cancellationToken).ConfigureAwait(false);
        var mapStyle = MapStyles.Normalize((await database.GetSettingAsync(MapStyleKey, cancellationToken).ConfigureAwait(false))?.Value);
        var (trackCount, filePaths) = await database.GetStorageSummaryAsync(cancellationToken).ConfigureAwait(false);
        var storageBytes = filePaths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Sum(path =>
            {
                try { return File.Exists(path) ? new FileInfo(path).Length : 0L; }
                catch { return 0L; }
            });
        return new AppSettings(theme, distance, elevation, trackCount, storageBytes, mapStyle);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await SaveValueAsync(ThemeKey, settings.Theme.ToString(), cancellationToken).ConfigureAwait(false);
        await SaveValueAsync(DistanceUnitKey, settings.DistanceUnit.ToString(), cancellationToken).ConfigureAwait(false);
        await SaveValueAsync(ElevationUnitKey, settings.ElevationUnit.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public Task SetDistanceUnitAsync(DistanceUnit unit, CancellationToken cancellationToken = default) =>
        SaveValueAsync(DistanceUnitKey, unit.ToString(), cancellationToken);

    public Task SetElevationUnitAsync(ElevationUnit unit, CancellationToken cancellationToken = default) =>
        SaveValueAsync(ElevationUnitKey, unit.ToString(), cancellationToken);

    public Task SetThemeAsync(ThemePreference theme, CancellationToken cancellationToken = default) =>
        SaveValueAsync(ThemeKey, theme.ToString(), cancellationToken);

    public Task SetMapStyleAsync(string styleId, CancellationToken cancellationToken = default) =>
        SaveValueAsync(MapStyleKey, MapStyles.Normalize(styleId), cancellationToken);

    private async Task<TEnum> GetEnumAsync<TEnum>(
        string key,
        TEnum fallback,
        CancellationToken cancellationToken)
        where TEnum : struct, Enum
    {
        var setting = await database.GetSettingAsync(key, cancellationToken).ConfigureAwait(false);
        return setting is not null && Enum.TryParse<TEnum>(setting.Value, true, out var value)
            ? value
            : fallback;
    }

    private Task SaveValueAsync(string key, string value, CancellationToken cancellationToken) =>
        database.SetSettingAsync(new Setting { Key = key, Value = value }, cancellationToken);
}
