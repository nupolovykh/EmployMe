using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Health;

/// <summary>
/// Reports the sources the ingest pipeline is failing against (EM-21). The
/// counters it reads are the ones IngestService already maintains; this check
/// only makes them visible at <c>/health/ready</c> instead of in a table
/// nobody looks at. Degraded, never Unhealthy: a broken upstream is a fact
/// about the world, not a reason for the platform to restart the API.
/// </summary>
public sealed class SourceHealthCheck(AppDbContext db) : IHealthCheck
{
    /// <summary>
    /// One failed run is a hiccup the retry pipeline did not absorb; three in
    /// a row is a source that is actually down, or an adapter that no longer
    /// matches its upstream.
    /// </summary>
    public const int FailureThreshold = 3;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failing = await db.Sources
            .Where(s => s.Enabled && s.ConsecutiveFailures >= FailureThreshold)
            .OrderBy(s => s.Slug)
            .Select(s => new { s.Slug, s.ConsecutiveFailures, s.LastSuccessAt, s.LastErrorAt })
            .ToListAsync(cancellationToken);

        if (failing.Count == 0)
        {
            return HealthCheckResult.Healthy("every enabled source succeeded on its last run");
        }

        var data = failing.ToDictionary(
            s => s.Slug,
            object (s) => new
            {
                consecutiveFailures = s.ConsecutiveFailures,
                lastSuccessAt = s.LastSuccessAt,
                lastErrorAt = s.LastErrorAt,
            });

        return HealthCheckResult.Degraded(
            $"{failing.Count} source(s) failing: {string.Join(", ", failing.Select(s => s.Slug))}",
            data: data);
    }
}
