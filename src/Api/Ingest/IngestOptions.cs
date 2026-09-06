namespace Api.Ingest;

public sealed class IngestOptions
{
    public const string SectionName = "Ingest";

    /// <summary>
    /// Whether this environment is reachable by the public. Sources not cleared
    /// for public display are skipped when it is, and the trigger token below
    /// becomes mandatory.
    /// <para>
    /// Nullable on purpose: left unset it resolves to "public unless
    /// Development", so forgetting to configure it fails safe. A deployment that
    /// silently ran with the guard off would be exactly the Phase I exit
    /// criterion — no Tier D connector enabled in a deployed environment —
    /// depending on someone remembering an environment variable.
    /// </para>
    /// </summary>
    public bool? PublicDeployment { get; set; }

    /// <summary>
    /// Shared secret required by the manual ingest endpoint on a public
    /// deployment. Without it, anyone could call the endpoint with force=true in
    /// a loop, which bypasses min_poll_interval and gets us banned by Jobicy.
    /// </summary>
    public string? TriggerToken { get; set; }

    /// <summary>Bounds a manual run against paginated sources like Arbeitnow.</summary>
    public int MaxPagesPerSource { get; set; } = 5;
}
