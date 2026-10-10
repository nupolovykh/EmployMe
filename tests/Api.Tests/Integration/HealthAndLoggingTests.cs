using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Health;
using Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Tests.Integration;

/// <summary>
/// What a platform probe and a log search see (EM-21): the HTTP status of
/// <c>/health/ready</c> in each state, and the log format outside Development.
/// </summary>
[Collection(PostgresCollection.Name)]
public class HealthAndLoggingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Ready_is_200_and_Degraded_when_a_source_keeps_failing()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using (var db = database.CreateContext())
        {
            var jobicy = await db.Sources.SingleAsync(s => s.Slug == "jobicy");
            jobicy.ConsecutiveFailures = SourceHealthCheck.FailureThreshold;
            await db.SaveChangesAsync();
        }

        await using var factory = new ApiFactory(database, triggerToken: null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var ready = await response.Content.ReadFromJsonAsync<ReadyShape>();

        // A broken upstream is a fact about the world, not about this process:
        // the instance must keep receiving traffic.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", ready!.Status);
        var sources = ready.Checks.Single(c => c.Name == "sources");
        Assert.Equal("Degraded", sources.Status);
        Assert.Contains("jobicy", sources.Description);
    }

    [Fact]
    public async Task Ready_is_503_when_the_database_is_unreachable_while_live_stays_200()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database, triggerToken: null);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

        // The API migrates on start, so the outage has to begin after it.
        var name = new NpgsqlConnectionStringBuilder(database.ConnectionString).Database!;
        await AdminAsync(database, $"""ALTER DATABASE "{name}" ALLOW_CONNECTIONS false""");
        await AdminAsync(database, $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{name}'");
        NpgsqlConnection.ClearAllPools();

        try
        {
            // Takes about 20 s, as it does in production: EnableRetryOnFailure
            // retries the connection before the check gives up and reports.
            var ready = await client.GetAsync("/health/ready");
            var live = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
            Assert.Equal("Unhealthy", (await ready.Content.ReadFromJsonAsync<ReadyShape>())!.Status);
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        }
        finally
        {
            await AdminAsync(database, $"""ALTER DATABASE "{name}" ALLOW_CONNECTIONS true""");
        }
    }

    [Fact]
    public async Task Outside_Development_every_log_line_is_one_JSON_event()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        var original = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(captured));

        try
        {
            await using var factory = new ApiFactory(database, triggerToken: null);
            using var client = factory.CreateClient();
            await client.GetAsync("/health");
        }
        finally
        {
            Console.SetOut(original);
        }

        var lines = captured.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            using var document = JsonDocument.Parse(line);
            Assert.True(document.RootElement.TryGetProperty("@t", out _), $"no timestamp: {line}");
            Assert.True(document.RootElement.TryGetProperty("@m", out _), $"no rendered message: {line}");
        });
        Assert.Contains(lines, line => line.Contains("\"RequestPath\":\"/health\""));
    }

    private static async Task AdminAsync(TestDatabase database, string sql)
    {
        var admin = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = "postgres", Pooling = false };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ReadyShape(string Status, List<CheckShape> Checks);
    private sealed record CheckShape(string Name, string Status, string? Description);
}
