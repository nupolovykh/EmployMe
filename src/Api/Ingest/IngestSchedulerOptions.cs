namespace Api.Ingest;

/// <summary>
/// How often the in-process scheduler asks whether any source is due. This is
/// not a poll interval and must not be read as one: which sources actually get
/// fetched on a tick is decided per row by <c>sources.min_poll_interval</c>,
/// inside <see cref="IngestService"/>, and a tick that finds nothing due
/// touches no upstream at all.
/// </summary>
public sealed class IngestSchedulerOptions
{
    public const string SectionName = "Ingest:Scheduler";

    /// <summary>
    /// Off for test hosts and for one-shot tooling; on everywhere a process
    /// that stays up is expected to keep the catalogue fresh.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Bounds how late a source can be fetched after its interval elapses. The
    /// shortest interval in the registry is one hour, so fifteen minutes puts
    /// every source within a quarter of its own cadence of being on time.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Lets the host finish starting — migrations are applied before the
    /// pipeline is built, but a platform health probe and the first user
    /// request should not queue behind four upstream fetches.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(30);
}
