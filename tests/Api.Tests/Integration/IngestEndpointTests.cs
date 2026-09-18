using System.Net;
using System.Net.Http.Json;
using Api.Tests.Support;

namespace Api.Tests.Integration;

/// <summary>
/// The guards that, until now, could only be checked against the deployed
/// instance (docs/PLAN.md, Phase I exit criterion).
/// </summary>
[Collection(PostgresCollection.Name)]
public class IngestEndpointTests(PostgresFixture postgres)
{
    // Himalayas is Tier C and refused before any adapter runs, so the request
    // exercises the endpoint without reaching a live source.
    private const string Ingest = "/api/ingest?source=himalayas";

    [Fact]
    public async Task Refuses_to_run_at_all_when_no_token_is_configured()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database, triggerToken: null);
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Ingest, null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task Rejects_a_missing_or_wrong_token(string? supplied)
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database, triggerToken: "expected");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Ingest);

        if (supplied is not null)
        {
            request.Headers.Add("X-Ingest-Token", supplied);
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Runs_with_the_right_token_and_reports_the_tier_refusal()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database, triggerToken: "expected");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingest-Token", "expected");

        var response = await client.PostAsync(Ingest, null);
        var report = await response.Content.ReadFromJsonAsync<ReportShape>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var source = Assert.Single(report!.Sources);
        Assert.Equal(("himalayas", "skipped"), (source.Slug, source.Outcome));
        Assert.Contains("tier C", source.Detail);
    }

    [Fact]
    public async Task Health_endpoints_answer()
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var factory = new ApiFactory(database, triggerToken: null);
        using var client = factory.CreateClient();

        var live = await client.GetAsync("/health");
        var ready = await client.GetFromJsonAsync<ReadyShape>("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", ready!.Status);
        Assert.Equal(["database", "sources"], ready.Checks.Select(c => c.Name).Order());
    }

    private sealed record ReportShape(List<SourceShape> Sources);
    private sealed record SourceShape(string Slug, string Outcome, string? Detail);
    private sealed record ReadyShape(string Status, List<CheckShape> Checks);
    private sealed record CheckShape(string Name, string Status);
}
