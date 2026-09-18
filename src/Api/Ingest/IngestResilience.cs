using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Api.Ingest;

/// <summary>
/// The Polly pipeline every upstream fetch goes through (EM-20). Tuned for
/// four keyless public APIs that are polled at most once an hour each: a
/// transient failure is retried a few times with backoff, a <c>429</c> is
/// retried when the source says when, and a source that keeps failing is cut
/// off for a while rather than hammered.
/// </summary>
public static class IngestResilience
{
    /// <summary>
    /// The whole request, retries included. Greenhouse with <c>content=true</c>
    /// on a large board is the slowest call observed; Arbeitnow pages are
    /// fetched one at a time inside this budget, not all together.
    /// </summary>
    public static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(120);

    public static void Configure(HttpStandardResilienceOptions options)
    {
        options.TotalRequestTimeout.Timeout = TotalTimeout;
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);

        // Three attempts beyond the first. The delay is doubled each time with
        // jitter, so a source that hiccups for a few seconds is not given up on
        // and one that is down for the hour is not asked more than four times.
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.Retry.Delay = TimeSpan.FromSeconds(2);

        // A Retry-After header wins over the backoff, capped: a source asking
        // us to come back in an hour is answered by the next scheduled run,
        // which the min_poll_interval already spaces out — not by holding a
        // Neon connection open for that hour (A-013).
        options.Retry.ShouldRetryAfterHeader = true;
        options.Retry.MaxDelay = TimeSpan.FromSeconds(30);

        // Retried: the transport failing, a timeout, 408, 429 and 5xx. A 404
        // is deliberately not — for a Tier A board it means the token is dead,
        // and the adapters already log and skip that per company.
        options.Retry.ShouldHandle = static args => ValueTask.FromResult(
            args.Outcome.Exception is HttpRequestException or TimeoutException
            || args.Outcome.Result is { } response && ShouldRetry(response.StatusCode));

        // The default breaker needs 100 requests in its sampling window before
        // it can open, which an ingest run never reaches — one Tier B source is
        // one to five requests an hour. Lowered so it can actually trip on a
        // Tier A run, where a dead ATS answers every board with the same 5xx.
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.FailureRatio = 0.8;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(5);
    }

    private static bool ShouldRetry(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || (int)status >= 500;
}
