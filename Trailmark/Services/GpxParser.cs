using System.Globalization;
using System.Xml;
using Trailmark.Models;

namespace Trailmark.Services;

public interface IGpxParser
{
    Task<GpxDocument> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}

public sealed class GpxParser : IGpxParser
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        Async = true,
        CloseInput = false,
        DtdProcessing = DtdProcessing.Prohibit,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true
    };

    public async Task<GpxDocument> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("The GPX stream must be readable.", nameof(stream));

        var document = new GpxDocument();
        try
        {
            using var reader = XmlReader.Create(stream, ReaderSettings);
            var foundRoot = false;
            var rootDepth = -1;

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                if (!foundRoot)
                {
                    if (!NameIs(reader, "gpx"))
                    {
                        AddIssue(document, reader, "The document root must be a GPX element.", true);
                        document.HasFatalError = true;
                        return document;
                    }

                    foundRoot = true;
                    rootDepth = reader.Depth;
                    continue;
                }

                if (reader.Depth != rootDepth + 1)
                    continue;

                if (NameIs(reader, "trk"))
                    document.Tracks.Add(await ParseTrackAsync(reader, document, cancellationToken).ConfigureAwait(false));
                else if (NameIs(reader, "rte"))
                    document.Tracks.Add(await ParseRouteAsync(reader, document, cancellationToken).ConfigureAwait(false));
                else if (NameIs(reader, "wpt"))
                {
                    var waypoint = await ParseWaypointAsync(reader, document, cancellationToken).ConfigureAwait(false);
                    if (waypoint is not null)
                        document.Waypoints.Add(waypoint);
                }
            }

            if (!foundRoot)
            {
                document.Issues.Add(new GpxParseIssue("The file is empty or does not contain a GPX root element.", IsError: true));
                document.HasFatalError = true;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (XmlException exception)
        {
            document.Issues.Add(new GpxParseIssue(
                $"Malformed GPX XML: {exception.Message}",
                exception.LineNumber,
                exception.LinePosition,
                true));
            document.HasFatalError = true;
        }

        return document;
    }

    private static async Task<ParsedTrack> ParseTrackAsync(
        XmlReader parentReader,
        GpxDocument document,
        CancellationToken cancellationToken)
    {
        var track = new ParsedTrack { Kind = TrackKind.Track };
        using var reader = parentReader.ReadSubtree();
        await reader.ReadAsync().ConfigureAwait(false);
        var rootDepth = reader.Depth;

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != rootDepth + 1)
                continue;

            if (NameIs(reader, "name"))
                track.Name = await ReadElementTextAsync(reader).ConfigureAwait(false);
            else if (NameIs(reader, "desc"))
                track.Description = await ReadElementTextAsync(reader).ConfigureAwait(false);
            else if (NameIs(reader, "trkseg"))
                track.Segments.Add(await ParseSegmentAsync(reader, document, cancellationToken).ConfigureAwait(false));
        }

        return track;
    }

    private static async Task<ParsedTrack> ParseRouteAsync(
        XmlReader parentReader,
        GpxDocument document,
        CancellationToken cancellationToken)
    {
        var route = new ParsedTrack { Kind = TrackKind.Route };
        var segment = new ParsedSegment();
        route.Segments.Add(segment);
        using var reader = parentReader.ReadSubtree();
        await reader.ReadAsync().ConfigureAwait(false);
        var rootDepth = reader.Depth;

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != rootDepth + 1)
                continue;

            if (NameIs(reader, "name"))
                route.Name = await ReadElementTextAsync(reader).ConfigureAwait(false);
            else if (NameIs(reader, "desc"))
                route.Description = await ReadElementTextAsync(reader).ConfigureAwait(false);
            else if (NameIs(reader, "rtept"))
            {
                var point = await ParsePointAsync(reader, document, cancellationToken).ConfigureAwait(false);
                if (point is not null)
                    segment.Points.Add(point);
            }
        }

        return route;
    }

    private static async Task<ParsedSegment> ParseSegmentAsync(
        XmlReader parentReader,
        GpxDocument document,
        CancellationToken cancellationToken)
    {
        var segment = new ParsedSegment();
        using var reader = parentReader.ReadSubtree();
        await reader.ReadAsync().ConfigureAwait(false);
        var rootDepth = reader.Depth;

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element
                && reader.Depth == rootDepth + 1
                && NameIs(reader, "trkpt"))
            {
                var point = await ParsePointAsync(reader, document, cancellationToken).ConfigureAwait(false);
                if (point is not null)
                    segment.Points.Add(point);
            }
        }

        return segment;
    }

    private static async Task<ParsedPoint?> ParsePointAsync(
        XmlReader parentReader,
        GpxDocument document,
        CancellationToken cancellationToken)
    {
        if (!TryReadCoordinates(parentReader, out var latitude, out var longitude))
        {
            AddIssue(document, parentReader, "Skipped a point with missing or invalid latitude/longitude.");
            return null;
        }

        double? elevation = null;
        DateTime? time = null;
        string? name = null;
        string? description = null;
        using var reader = parentReader.ReadSubtree();
        await reader.ReadAsync().ConfigureAwait(false);
        var rootDepth = reader.Depth;

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != rootDepth + 1)
                continue;

            if (NameIs(reader, "ele"))
            {
                var value = await ReadElementTextAsync(reader).ConfigureAwait(false);
                if (TryParseDouble(value, out var parsed))
                    elevation = parsed;
                else
                    AddIssue(document, reader, "Ignored an invalid elevation value.");
            }
            else if (NameIs(reader, "time"))
            {
                var value = await ReadElementTextAsync(reader).ConfigureAwait(false);
                if (TryParseUtc(value, out var parsed))
                    time = parsed;
                else
                    AddIssue(document, reader, "Ignored an invalid timestamp.");
            }
            else if (NameIs(reader, "name"))
                name = await ReadElementTextAsync(reader).ConfigureAwait(false);
            else if (NameIs(reader, "desc"))
                description = await ReadElementTextAsync(reader).ConfigureAwait(false);
        }

        return new ParsedPoint(latitude, longitude, elevation, time, name, description);
    }

    private static async Task<ParsedWaypoint?> ParseWaypointAsync(
        XmlReader parentReader,
        GpxDocument document,
        CancellationToken cancellationToken)
    {
        if (!TryReadCoordinates(parentReader, out var latitude, out var longitude))
        {
            AddIssue(document, parentReader, "Skipped a waypoint with missing or invalid latitude/longitude.");
            return null;
        }

        double? elevation = null;
        DateTime? time = null;
        string? name = null;
        string? description = null;
        string? symbol = null;
        using var reader = parentReader.ReadSubtree();
        await reader.ReadAsync().ConfigureAwait(false);
        var rootDepth = reader.Depth;

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != rootDepth + 1)
                continue;

            if (NameIs(reader, "ele"))
            {
                var value = await ReadElementTextAsync(reader).ConfigureAwait(false);
                if (TryParseDouble(value, out var parsed))
                    elevation = parsed;
                else
                    AddIssue(document, reader, "Ignored an invalid waypoint elevation.");
            }
            else if (NameIs(reader, "time"))
            {
                var value = await ReadElementTextAsync(reader).ConfigureAwait(false);
                if (TryParseUtc(value, out var parsed))
                    time = parsed;
                else
                    AddIssue(document, reader, "Ignored an invalid waypoint timestamp.");
            }
            else if (NameIs(reader, "name"))
                name = await ReadElementTextAsync(reader).ConfigureAwait(false);
            else if (NameIs(reader, "desc"))
                description = await ReadElementTextAsync(reader).ConfigureAwait(false);
            else if (NameIs(reader, "sym"))
                symbol = await ReadElementTextAsync(reader).ConfigureAwait(false);
        }

        return new ParsedWaypoint(latitude, longitude, elevation, time, name, description, symbol);
    }

    private static async Task<string?> ReadElementTextAsync(XmlReader parentReader)
    {
        using var reader = parentReader.ReadSubtree();
        await reader.ReadAsync().ConfigureAwait(false);
        var value = await reader.ReadElementContentAsStringAsync().ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool TryReadCoordinates(XmlReader reader, out double latitude, out double longitude)
    {
        var hasLatitude = TryParseDouble(reader.GetAttribute("lat"), out latitude);
        var hasLongitude = TryParseDouble(reader.GetAttribute("lon"), out longitude);
        return hasLatitude && hasLongitude
            && latitude is >= -90 and <= 90
            && longitude is >= -180 and <= 180;
    }

    private static bool TryParseDouble(string? value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
        && double.IsFinite(result);

    private static bool TryParseUtc(string? value, out DateTime result)
    {
        if (DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var parsed))
        {
            result = parsed.UtcDateTime;
            return true;
        }

        result = default;
        return false;
    }

    private static bool NameIs(XmlReader reader, string localName) =>
        string.Equals(reader.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    private static void AddIssue(GpxDocument document, XmlReader reader, string message, bool isError = false)
    {
        var lineInfo = reader as IXmlLineInfo;
        document.Issues.Add(new GpxParseIssue(
            message,
            lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : null,
            lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : null,
            isError));
    }
}
