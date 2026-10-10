using Api.Data;
using Api.Health;
using Api.Ingest;
using Api.Models;
using Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Api.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class IngestServiceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static IngestService Service(AppDbContext db, FakeTimeProvider clock, bool publicDeployment, params IJobSource[] adapters) =>
        new(db, adapters, Options.Create(new IngestOptions { PublicDeployment = publicDeployment }), clock, NullLogger<IngestService>.Instance);

    /// <summary>
    /// One run, one context — the way the controller (scoped per request) and
    /// the scheduler (a scope per tick) actually use the service.
    /// </summary>
    private static async Task<SourceIngestResult> RunAsync(
        TestDatabase database, FakeTimeProvider clock, string slug, bool force, params IJobSource[] adapters)
    {
        await using var db = database.CreateContext();
        var report = await Service(db, clock, publicDeployment: false, adapters).RunAsync(slug, force, CancellationToken.None);
        return report.Sources.Single();
    }

    [Fact]
    public async Task Persists_postings_once_and_updates_them_on_the_next_run()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        var clock = new FakeTimeProvider(Start);
        var adapter = FakeJobSource.Returning("jobicy", 3);

        await using (var db = database.CreateContext())
        {
            var first = await Service(db, clock, publicDeployment: false, adapter).RunAsync("jobicy", force: true, CancellationToken.None);
            Assert.Equal(("ok", 3, 3, 0), (first.Sources[0].Outcome, first.Fetched, first.Created, first.Updated));
        }

        await using (var db = database.CreateContext())
        {
            var second = await Service(db, clock, publicDeployment: false, adapter).RunAsync("jobicy", force: true, CancellationToken.None);
            Assert.Equal(("ok", 3, 0, 3), (second.Sources[0].Outcome, second.Fetched, second.Created, second.Updated));

            // EM-58: one raw row per posting, however many times it is fetched.
            Assert.Equal(3, await db.Vacancies.CountAsync());
            Assert.Equal(3, await db.RawPostings.CountAsync());

            var source = await db.Sources.SingleAsync(s => s.Slug == "jobicy");
            Assert.Equal(Start, source.LastSuccessAt);
            Assert.Equal(0, source.ConsecutiveFailures);
        }
    }

    [Fact]
    public async Task Honours_min_poll_interval_unless_forced()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        var clock = new FakeTimeProvider(Start);
        var adapter = FakeJobSource.Returning("jobicy", 1);

        await RunAsync(database, clock, "jobicy", force: false, adapter);
        var tooSoon = await RunAsync(database, clock, "jobicy", force: false, adapter);

        Assert.Equal("skipped", tooSoon.Outcome);
        Assert.Contains("min poll interval", tooSoon.Detail);
        Assert.Equal(1, adapter.Calls);

        TimeSpan interval;
        await using (var db = database.CreateContext())
        {
            interval = (await db.Sources.SingleAsync(s => s.Slug == "jobicy")).MinPollInterval;
        }

        clock.Advance(interval + TimeSpan.FromMinutes(1));

        var due = await RunAsync(database, clock, "jobicy", force: false, adapter);
        Assert.Equal("ok", due.Outcome);
        Assert.Equal(2, adapter.Calls);

        var forced = await RunAsync(database, clock, "jobicy", force: true, adapter);
        Assert.Equal("ok", forced.Outcome);
        Assert.Equal(3, adapter.Calls);
    }

    [Theory]
    [InlineData("himalayas", SourceTier.C)]
    [InlineData("himalayas", SourceTier.D)]
    public async Task Tier_c_and_d_rows_are_refused_by_tier_even_when_forced(string slug, SourceTier tier)
    {
        // A fresh database seeds no Tier D row at all (the deployed one has
        // none either), so the Tier D case is the Himalayas row moved down.
        await using var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateContext();

        // Flip every mutable flag on: the refusal must not rest on them.
        await db.Sources.Where(s => s.Slug == slug)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Tier, tier)
                .SetProperty(x => x.Enabled, true)
                .SetProperty(x => x.PublicDeployEnabled, true));

        var adapter = FakeJobSource.Returning((await db.Sources.SingleAsync(s => s.Slug == slug)).AdapterType, 5);
        var report = await Service(db, new FakeTimeProvider(Start), publicDeployment: false, adapter).RunAsync(slug, force: true, CancellationToken.None);

        Assert.Equal("skipped", report.Sources[0].Outcome);
        Assert.Contains("never ingested", report.Sources[0].Detail);
        Assert.Equal(0, adapter.Calls);
        Assert.Equal(0, await db.Vacancies.CountAsync());
    }

    [Fact]
    public async Task A_source_not_cleared_for_public_display_is_skipped_on_a_public_deployment()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateContext();
        await db.Sources.Where(s => s.Slug == "jobicy").ExecuteUpdateAsync(s => s.SetProperty(x => x.PublicDeployEnabled, false));
        var adapter = FakeJobSource.Returning("jobicy", 1);

        var onPublic = await Service(db, new FakeTimeProvider(Start), publicDeployment: true, adapter).RunAsync("jobicy", force: true, CancellationToken.None);
        Assert.Equal("skipped", onPublic.Sources[0].Outcome);
        Assert.Contains("public deployment", onPublic.Sources[0].Detail);

        var locally = await Service(db, new FakeTimeProvider(Start), publicDeployment: false, adapter).RunAsync("jobicy", force: true, CancellationToken.None);
        Assert.Equal("ok", locally.Sources[0].Outcome);
    }

    [Fact]
    public async Task A_failing_source_is_counted_and_does_not_stop_the_run()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        var clock = new FakeTimeProvider(Start);
        var broken = FakeJobSource.Throwing("jobicy", new HttpRequestException("upstream down"));
        var working = FakeJobSource.Returning("arbeitnow", 2);

        await using var db = database.CreateContext();
        var report = await Service(db, clock, publicDeployment: false, broken, working).RunAsync(null, force: true, CancellationToken.None);

        var bySlug = report.Sources.ToDictionary(s => s.Slug);
        Assert.Equal("failed", bySlug["jobicy"].Outcome);
        Assert.Equal("see server logs", bySlug["jobicy"].Detail);
        Assert.Equal("ok", bySlug["arbeitnow"].Outcome);
        Assert.Equal(2, await db.Vacancies.CountAsync());

        var jobicy = await db.Sources.AsNoTracking().SingleAsync(s => s.Slug == "jobicy");
        Assert.Equal(1, jobicy.ConsecutiveFailures);
        Assert.Equal(Start, jobicy.LastErrorAt);
        Assert.Null(jobicy.LastSuccessAt);
    }

    [Fact]
    public async Task Source_health_degrades_after_three_consecutive_failures_and_recovers()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        var clock = new FakeTimeProvider(Start);
        var broken = FakeJobSource.Throwing("jobicy", new HttpRequestException("upstream down"));

        await using var db = database.CreateContext();

        for (var i = 0; i < SourceHealthCheck.FailureThreshold; i++)
        {
            await Service(db, clock, publicDeployment: false, broken).RunAsync("jobicy", force: true, CancellationToken.None);
        }

        var degraded = await new SourceHealthCheck(db).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, degraded.Status);
        Assert.Contains("jobicy", degraded.Description);
        Assert.True(degraded.Data.ContainsKey("jobicy"));

        await Service(db, clock, publicDeployment: false, FakeJobSource.Returning("jobicy", 1)).RunAsync("jobicy", force: true, CancellationToken.None);

        var healthy = await new SourceHealthCheck(db).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, healthy.Status);
    }

    [Fact]
    public async Task Migrations_seed_the_registry_the_plan_describes()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateContext();

        var sources = await db.Sources.OrderBy(s => s.Id).ToListAsync();

        // No hh.ru row: RebuildSourcesSchema carried one over only where the
        // pre-Revision-2 schema had it, and a fresh database never did.
        Assert.Equal(["greenhouse", "lever", "jobicy", "arbeitnow", "himalayas"], sources.Select(s => s.Slug));
        Assert.DoesNotContain(sources, s => s.Tier == SourceTier.D);
        Assert.All(sources.Where(s => s.Tier is SourceTier.C or SourceTier.D), s => Assert.False(s.PublicDeployEnabled));
        Assert.All(sources.Where(s => s.Enabled), s => Assert.True(s.MinPollInterval >= TimeSpan.FromHours(1)));
        Assert.Equal(4, sources.Count(s => s.Enabled && s.PublicDeployEnabled));
    }
}
