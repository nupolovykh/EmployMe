using System.Collections.Concurrent;
using Api.Data;
using Api.Ingest;
using Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Api.Tests.Integration;

/// <summary>
/// The scheduler (EM-18) adds no policy of its own: it supplies ticks, and
/// <c>min_poll_interval</c> inside <see cref="IngestService"/> decides what is
/// fetched. These tests drive it with a fake clock against a real database.
/// </summary>
[Collection(PostgresCollection.Name)]
public class IngestSchedulerTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly IngestSchedulerOptions Settings = new()
    {
        StartupDelay = TimeSpan.FromSeconds(30),
        Interval = TimeSpan.FromMinutes(15),
    };

    [Fact]
    public async Task First_tick_waits_for_the_startup_delay_then_the_source_interval_decides()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        var clock = new CountingClock(Start);
        var jobicy = FakeJobSource.Returning("jobicy", 2);
        var log = new ListLogger<IngestScheduler>();
        using var services = Services(database, clock, jobicy, failFirstScopes: 0);
        using var stop = new CancellationTokenSource();

        var scheduler = new IngestScheduler(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(Settings), clock, log);
        await scheduler.StartAsync(stop.Token);
        await Eventually(() => clock.Timers >= 1, log);

        clock.Advance(TimeSpan.FromSeconds(29));
        await Task.Delay(200);
        Assert.Equal(0, jobicy.Calls);

        // t = 30 s: the first tick fetches. Jobicy's interval is one hour.
        await TickAsync(clock, TimeSpan.FromSeconds(1), log, expectedSummaries: 1);
        Assert.Equal(1, jobicy.Calls);
        await Eventually(() => clock.Timers >= 2, log);

        // t = 15, 30, 45 min after it: inside the interval, skipped.
        for (var tick = 2; tick <= 4; tick++)
        {
            await TickAsync(clock, Settings.Interval, log, expectedSummaries: tick);
        }

        Assert.Equal(1, jobicy.Calls);
        Assert.Contains("jobicy=skipped", log.Messages.Last());

        // t = 60 min after the first fetch: due again.
        await TickAsync(clock, Settings.Interval, log, expectedSummaries: 5);
        Assert.Equal(2, jobicy.Calls);
        Assert.Contains("jobicy=ok", log.Messages.Last());

        await stop.CancelAsync();
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_failed_tick_is_logged_and_the_next_tick_still_runs()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        var clock = new CountingClock(Start);
        var jobicy = FakeJobSource.Returning("jobicy", 1);
        var log = new ListLogger<IngestScheduler>();
        using var services = Services(database, clock, jobicy, failFirstScopes: 1);
        using var stop = new CancellationTokenSource();

        var scheduler = new IngestScheduler(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(Settings), clock, log);
        await scheduler.StartAsync(stop.Token);
        await Eventually(() => clock.Timers >= 1, log);

        clock.Advance(Settings.StartupDelay);
        await Eventually(() => log.Errors.Count == 1, log);
        Assert.Contains("Scheduled ingest run failed", log.Errors.Single());
        Assert.Equal(0, jobicy.Calls);
        await Eventually(() => clock.Timers >= 2, log);

        await TickAsync(clock, Settings.Interval, log, expectedSummaries: 1);
        Assert.Equal(1, jobicy.Calls);

        await stop.CancelAsync();
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Disabled_scheduler_never_ticks()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        var clock = new CountingClock(Start);
        var jobicy = FakeJobSource.Returning("jobicy", 1);
        var log = new ListLogger<IngestScheduler>();
        using var services = Services(database, clock, jobicy, failFirstScopes: 0);

        var disabled = new IngestSchedulerOptions { Enabled = false };
        var scheduler = new IngestScheduler(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(disabled), clock, log);
        await scheduler.StartAsync(CancellationToken.None);

        clock.Advance(TimeSpan.FromHours(2));
        await Task.Delay(200);

        Assert.Equal(0, jobicy.Calls);
        Assert.Contains("Ingest scheduler is disabled", log.Messages);
        await scheduler.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// The scheduler's real dependencies, scoped the way Program.cs scopes
    /// them. Only Jobicy has an adapter; the other seeded sources report
    /// "no adapter" and are skipped. The first <paramref name="failFirstScopes"/>
    /// ticks get a context on a database that does not exist, so the run fails
    /// on its first query — as it does when the real database is unreachable.
    /// </summary>
    private static ServiceProvider Services(TestDatabase database, CountingClock clock, IJobSource adapter, int failFirstScopes)
    {
        var failuresLeft = failFirstScopes;
        var missing = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = "employme_missing" }.ConnectionString;
        var services = new ServiceCollection();

        services.AddScoped<AppDbContext>(_ => Interlocked.Decrement(ref failuresLeft) >= 0
            ? new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(missing, o => o.UseVector()).Options)
            : database.CreateContext());
        services.AddSingleton(adapter);
        services.AddSingleton(Options.Create(new IngestOptions { PublicDeployment = false }));
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<ILogger<IngestService>>(NullLogger<IngestService>.Instance);
        services.AddScoped<IngestService>();

        return services.BuildServiceProvider();
    }

    private static async Task TickAsync(CountingClock clock, TimeSpan advance, ListLogger<IngestScheduler> log, int expectedSummaries)
    {
        clock.Advance(advance);
        await Eventually(() => log.Summaries.Count >= expectedSummaries, log);
    }

    private static async Task Eventually(Func<bool> condition, ListLogger<IngestScheduler> log)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "the scheduler did not tick in time; its log: " + string.Join(" | ", log.Messages));
            await Task.Delay(20);
        }
    }

    /// <summary>
    /// Since .NET 10 a BackgroundService runs ExecuteAsync on the thread pool,
    /// so the test has to know the scheduler has created its timer before it
    /// moves the clock; otherwise the advance lands before the timer exists
    /// and never fires.
    /// </summary>
    private sealed class CountingClock(DateTimeOffset start) : FakeTimeProvider(start)
    {
        private int _timers;

        public int Timers => Volatile.Read(ref _timers);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Interlocked.Increment(ref _timers);
            return base.CreateTimer(callback, state, dueTime, period);
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        public IReadOnlyList<string> Messages => _entries.Select(e => e.Message).ToList();

        public IReadOnlyList<string> Summaries => Messages.Where(m => m.StartsWith("Scheduled ingest:")).ToList();

        public IReadOnlyList<string> Errors => _entries.Where(e => e.Level == LogLevel.Error).Select(e => e.Message).ToList();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
