using System.Text.Json;
using Trailmark.Data;
using Trailmark.Models;

namespace Trailmark.Services;

public sealed class MapTilerOptions
{
    public const string OutdoorStyleId = "outdoor-v2";

    public MapTilerOptions(string apiKey, string styleId = OutdoorStyleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleId);
        ApiKey = apiKey?.Trim() ?? string.Empty;
        StyleId = styleId;
    }

    public string ApiKey { get; }
    public string StyleId { get; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public string GetStyleUrl() =>
        GetStyleUrl(StyleId);

    public string GetStyleUrl(string styleId) =>
        $"https://api.maptiler.com/maps/{Uri.EscapeDataString(styleId)}/style.json?key={Uri.EscapeDataString(ApiKey)}";
}

/// <summary>The MapTiler styles the settings screen can switch between.</summary>
public static class MapStyles
{
    public const string Default = MapTilerOptions.OutdoorStyleId;

    public static readonly IReadOnlyList<(string Id, string Name, string Description)> All =
    [
        (MapTilerOptions.OutdoorStyleId, "Outdoor", "Terrain, contours and trails"),
        ("topo-v2", "Topographic", "Detailed contour lines"),
        ("streets-v2", "Streets", "Roads and city labels"),
        ("satellite", "Satellite", "Aerial imagery")
    ];

    public static string NameFor(string? styleId)
    {
        foreach (var style in All)
        {
            if (string.Equals(style.Id, styleId, StringComparison.OrdinalIgnoreCase))
                return style.Name;
        }

        return All[0].Name;
    }

    public static string Normalize(string? styleId) =>
        All.Any(style => string.Equals(style.Id, styleId, StringComparison.OrdinalIgnoreCase))
            ? styleId!
            : Default;
}

public interface IMapTilerService
{
    Task<string> CreateTrackMapHtmlAsync(
        long trackId,
        string? styleId = null,
        LocationResult? currentLocation = null,
        CancellationToken cancellationToken = default);
}

public sealed class MapTilerService(ITrailmarkDatabase database, MapTilerOptions options) : IMapTilerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<string> CreateTrackMapHtmlAsync(
        long trackId,
        string? styleId = null,
        LocationResult? currentLocation = null,
        CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
            return """
                <!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1">
                <style>body{margin:0;height:100vh;display:grid;place-items:center;background:#d8e1ce;color:#263f29;font:15px sans-serif;text-align:center}p{max-width:280px;line-height:1.5}</style></head>
                <body><p><strong>MapTiler key required</strong><br>Copy MapTiler.local.props.example to Trailmark/MapTiler.local.props and add your local API key.</p></body></html>
                """;

        var track = await database.GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Track {trackId} was not found.");
        var points = await database.GetTrackPointsAsync(trackId, cancellationToken).ConfigureAwait(false);
        var waypoints = await database.GetWaypointsAsync(trackId, cancellationToken).ConfigureAwait(false);

        var features = new List<object>();
        foreach (var segment in points.GroupBy(point => point.SegmentIndex).OrderBy(group => group.Key))
        {
            var coordinates = segment
                .OrderBy(point => point.SegmentSequence)
                .Select(point => new[] { point.Longitude, point.Latitude })
                .ToArray();
            if (coordinates.Length >= 2)
            {
                features.Add(new
                {
                    type = "Feature",
                    properties = new { segment = segment.Key },
                    geometry = new { type = "LineString", coordinates }
                });
            }
        }

        foreach (var waypoint in waypoints)
        {
            features.Add(new
            {
                type = "Feature",
                properties = new { name = waypoint.Name ?? string.Empty, symbol = waypoint.Symbol ?? string.Empty },
                geometry = new { type = "Point", coordinates = new[] { waypoint.Longitude, waypoint.Latitude } }
            });
        }

        var allCoordinates = points.Select(point => (point.Longitude, point.Latitude))
            .Concat(waypoints.Select(waypoint => (waypoint.Longitude, waypoint.Latitude)))
            .ToList();
        object? currentLocationFeature = null;
        if (currentLocation is { IsSuccess: true, Latitude: { } latitude, Longitude: { } longitude })
        {
            currentLocationFeature = new
            {
                type = "Feature",
                properties = new Dictionary<string, object>(),
                geometry = new { type = "Point", coordinates = new[] { longitude, latitude } }
            };
            allCoordinates.Add((longitude, latitude));
        }

        var featureCollection = new { type = "FeatureCollection", features };
        var bounds = CreateBounds(allCoordinates);
        var resolvedStyleId = MapStyles.Normalize(styleId ?? options.StyleId);
        return HtmlTemplate
            .Replace("__STYLE_URL__", JsonSerializer.Serialize(options.GetStyleUrl(resolvedStyleId)), StringComparison.Ordinal)
            .Replace("__TRACK_NAME__", JsonSerializer.Serialize(track.Name), StringComparison.Ordinal)
            .Replace("__TRACK_DATA__", JsonSerializer.Serialize(featureCollection, JsonOptions), StringComparison.Ordinal)
            .Replace("__CURRENT_LOCATION__", JsonSerializer.Serialize(currentLocationFeature, JsonOptions), StringComparison.Ordinal)
            .Replace("__BOUNDS__", JsonSerializer.Serialize(bounds), StringComparison.Ordinal);
    }

    private static double[][]? CreateBounds(IReadOnlyCollection<(double Longitude, double Latitude)> coordinates)
    {
        if (coordinates.Count == 0)
            return null;

        return
        [
            [coordinates.Min(item => item.Longitude), coordinates.Min(item => item.Latitude)],
            [coordinates.Max(item => item.Longitude), coordinates.Max(item => item.Latitude)]
        ];
    }

    private const string HtmlTemplate = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no">
  <link rel="stylesheet" href="https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.css">
  <style>
    html, body, #map { width: 100%; height: 100%; margin: 0; background: #e8ecef; }
    .maplibregl-ctrl-attrib { font-size: 10px; }
  </style>
</head>
<body>
<div id="map" role="application" aria-label="Track map"></div>
<script src="https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.js"></script>
<script>
  const styleUrl = __STYLE_URL__;
  const trackName = __TRACK_NAME__;
  const trackData = __TRACK_DATA__;
  const currentLocation = __CURRENT_LOCATION__;
  const bounds = __BOUNDS__;
  const map = new maplibregl.Map({
    container: 'map',
    style: styleUrl,
    center: bounds ? [(bounds[0][0] + bounds[1][0]) / 2, (bounds[0][1] + bounds[1][1]) / 2] : [0, 0],
    zoom: bounds ? 12 : 2,
    attributionControl: true
  });
  map.addControl(new maplibregl.NavigationControl({ showCompass: true }), 'top-right');
  window.trailmark = {
    zoomToTrack: () => {
      if (!bounds) return;
      const samePoint = bounds[0][0] === bounds[1][0] && bounds[0][1] === bounds[1][1];
      if (samePoint) map.easeTo({ center: bounds[0], zoom: 15 });
      else map.fitBounds(bounds, { padding: 48, maxZoom: 16 });
    },
    recenter: (latitude, longitude) => {
      map.easeTo({ center: [longitude, latitude], zoom: Math.max(map.getZoom(), 15) });
      const source = map.getSource('trailmark-location');
      const feature = { type: 'Feature', properties: {}, geometry: { type: 'Point', coordinates: [longitude, latitude] } };
      if (source) source.setData(feature);
    }
  };

  map.on('load', () => {
    map.addSource('trailmark-track', { type: 'geojson', data: trackData });
    map.addLayer({
      id: 'trailmark-route-casing', type: 'line', source: 'trailmark-track',
      filter: ['==', ['geometry-type'], 'LineString'],
      paint: { 'line-color': '#ffffff', 'line-width': 7, 'line-opacity': 0.85 }
    });
    map.addLayer({
      id: 'trailmark-route', type: 'line', source: 'trailmark-track',
      filter: ['==', ['geometry-type'], 'LineString'],
      paint: { 'line-color': '#e85d35', 'line-width': 4 }
    });
    map.addLayer({
      id: 'trailmark-waypoints', type: 'circle', source: 'trailmark-track',
      filter: ['==', ['geometry-type'], 'Point'],
      paint: { 'circle-radius': 6, 'circle-color': '#e85d35', 'circle-stroke-width': 2, 'circle-stroke-color': '#ffffff' }
    });

    if (currentLocation) {
      map.addSource('trailmark-location', { type: 'geojson', data: currentLocation });
      map.addLayer({
        id: 'trailmark-location', type: 'circle', source: 'trailmark-location',
        paint: { 'circle-radius': 7, 'circle-color': '#1976d2', 'circle-stroke-width': 3, 'circle-stroke-color': '#ffffff' }
      });
    }

    if (bounds) {
      const samePoint = bounds[0][0] === bounds[1][0] && bounds[0][1] === bounds[1][1];
      if (samePoint) map.jumpTo({ center: bounds[0], zoom: 15 });
      else map.fitBounds(bounds, { padding: 48, maxZoom: 16, duration: 0 });
    }
  });
  document.title = trackName;
</script>
</body>
</html>
""";
}
