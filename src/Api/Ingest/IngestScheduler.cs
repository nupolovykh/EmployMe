using Microsoft.Extensions.Options;

namespace Api.Ingest;

/// <summary>
/// Scheduled ingest (EM-18). Every tick runs the same source-agnostic command
/// the manual endpoint runs, with <c>force=false</c>, so the per-source
/// <c>min_poll_interval</c> and the per-source advisory lock (A-012) govern
/// what is fetched exactly as they do for a manual run. The scheduler adds no
/// policy of its own; it only supplies the ticks.
/// <para>
/// On a host that sleeps when idle — Render's free instance — this service
/// sleeps with it. The GitHub Actions workflow in <c>.github/workflows/ingest.yml</c>
/// covers that case by calling the manual endpoint on a cron, which both wakes
/// the instance and runs the ingest; see A-014.
/// </para>
/// </summary>
public sealed class IngestScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestSchedulerOptions> options,
    TimeProvider clock,
    ILogger<IngestScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            logger.LogInformation("Ingest scheduler is disabled");
            return;
        }

        logger.LogInformation(
            "Ingest scheduler ticking every {Interval} after a {StartupDelay} delay",
            settings.Interval, settings.StartupDelay);

        try
        {
            await Task.Delay(settings.StartupDelay, clock, stoppingToken);

            using var timer = new PeriodicTimer(settings.Interval, clock);

            do
            {
                await TickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown; nothing to report.
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        // A scope per tick: IngestService is scoped because AppDbContext is, and
        // a context that lived for the life of the process would keep every
        // vacancy it ever tracked.
        using var scope = scopeFactory.CreateScope();
        var ingest = scope.ServiceProvider.GetRequiredService<IngestService>();

        try
        {
            var report = await ingest.RunAsync(sourceSlug: null, force: false, stoppingToken);

            // RunAsync already logs each source's counts and each failure in
            // full. The tick summary is what a log search for the scheduler
            // itself should find: one line, with the outcomes.
            logger.LogInformation(
                "Scheduled ingest: {Fetched} fetched, {Created} created, {Updated} updated; {Outcomes}",
                report.Fetched, report.Created, report.Updated,
                string.Join(", ", report.Sources.Select(s => $"{s.Slug}={s.Outcome}")));
        }
        // RunAsync contains per-source failures itself; what reaches here is
        // the run as a whole failing before any source ran — the database
        // being unreachable, typically. Logged, not rethrown: a BackgroundService
        // that throws stops for good, and the next tick may well succeed.
        catch (Exception ex) when (!(ex is OperationCanceledException && stoppingToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Scheduled ingest run failed");
        }
    }
}
