namespace Api.Ingest;

public sealed class IngestOptions
{
    public const string SectionName = "Ingest";

    /// <summary>
    /// True for any environment the public can reach. Sources not cleared for
    /// public display are skipped when this is set — see the Phase I exit
    /// criterion and docs/SOURCES.md.
    /// </summary>
    public bool PublicDeployment { get; set; }

    /// <summary>Bounds a manual run against paginated sources like Arbeitnow.</summary>
    public int MaxPagesPerSource { get; set; } = 5;
}
