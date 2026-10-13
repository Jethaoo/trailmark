namespace Trailmark.Models;

public enum ImportStatus
{
    Success,
    Cancelled,
    InvalidFile,
    Failed
}

public sealed record ImportResult(
    ImportStatus Status,
    IReadOnlyList<long> TrackIds,
    IReadOnlyList<GpxParseIssue> Issues,
    string? ErrorMessage = null)
{
    public bool IsSuccess => Status == ImportStatus.Success;

    public static ImportResult Cancelled { get; } = new(
        ImportStatus.Cancelled,
        Array.Empty<long>(),
        Array.Empty<GpxParseIssue>());
}
