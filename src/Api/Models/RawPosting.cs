namespace Api.Models;

/// <summary>
/// The most recent fetch of each posting, stored verbatim before mapping, so a
/// mapping bug is replayable without re-hitting a rate-limited source.
/// <para>
/// One row per posting, not per fetch — a re-run overwrites rather than
/// appends. Appending made the table grow with ingest frequency instead of with
/// the size of the catalogue, which a scheduled hourly run turns into a full
/// database in days (EM-58). Replaying a mapping bug needs a posting's current
/// shape, not its history, so nothing that matters is lost.
/// </para>
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
