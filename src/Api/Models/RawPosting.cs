namespace Api.Models;

/// <summary>
/// Every fetch stored verbatim before mapping, so a mapping bug is replayable
/// without re-hitting a rate-limited source. Append-only: one row per fetch,
/// not deduplicated by ExternalId.
/// </summary>
public class RawPosting
{
    public int Id { get; set; }

    public int SourceId { get; set; }
    public Source? Source { get; set; }

    public required string ExternalId { get; set; }
    public required string Payload { get; set; }

    public DateTimeOffset FetchedAt { get; set; }
}
