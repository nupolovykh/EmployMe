namespace Api.Ingest;

public sealed record IngestReport(IReadOnlyList<SourceIngestResult> Sources)
{
    public int Fetched => Sources.Sum(s => s.Fetched);
    public int Created => Sources.Sum(s => s.Created);
    public int Updated => Sources.Sum(s => s.Updated);
}

public sealed record SourceIngestResult(
    string Slug,
    string Outcome,
    int Fetched = 0,
    int Created = 0,
    int Updated = 0,
    string? Detail = null);
