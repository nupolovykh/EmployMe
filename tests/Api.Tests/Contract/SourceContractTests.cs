using Api.Data;
using Api.Ingest;
using Api.Ingest.Adapters;
using Api.Models;
using Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests.Contract;

/// <summary>
/// The nightly source contract test (EM-55, PLAN.md §01 rule 7). Each enabled
/// source's real adapter is run against its real endpoint, using the row and
/// registry the migrations seed, and must yield postings whose required
/// fields are present. An upstream closing, moving, or changing shape fails
/// this within a day instead of three weeks — hh.ru's 403 sat unnoticed for
/// that long.
/// <para>
/// Tagged <c>Category=Contract</c> and excluded from the per-PR run: it
/// needs the network, and Jobicy's cap is one poll per hour.
/// </para>
/// </summary>
[Trait("Category", "Contract")]
[Collection(PostgresCollection.Name)]
public class SourceContractTests(PostgresFixture postgres)
{
    private static readonly IHttpClientFactory Http = BuildHttp();

    private static IHttpClientFactory BuildHttp()
    {
        var services = new ServiceCollection();

        services.AddHttpClient(IngestHttp.ClientName, client =>
            {
                client.Timeout = IngestResilience.TotalTimeout + TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("EmployMe-contract-test/0.1 (+https://github.com/nupolovykh/EmployMe)");
            })
            .AddStandardResilienceHandler(IngestResilience.Configure)
            .SelectPipelineByAuthority();

        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
    }

    private static IJobSource Adapter(string adapterType) => adapterType switch
    {
        "greenhouse" => new GreenhouseJobSource(Http, NullLogger<GreenhouseJobSource>.Instance),
        "lever" => new LeverJobSource(Http, NullLogger<LeverJobSource>.Instance),
        "jobicy" => new JobicyJobSource(Http),
        "arbeitnow" => new ArbeitnowJobSource(Http, Options.Create(new IngestOptions { MaxPagesPerSource = 1 })),
        _ => throw new InvalidOperationException($"no adapter for '{adapterType}' — add it here when a source is added"),
    };

    [Theory]
    [InlineData("greenhouse")]
    [InlineData("lever")]
    [InlineData("jobicy")]
    [InlineData("arbeitnow")]
    public async Task Enabled_source_still_answers_with_postings_the_adapter_can_map(string slug)
    {
        await using var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateContext();

        var source = await db.Sources.AsNoTracking().SingleAsync(s => s.Slug == slug);
        Assert.True(source.Enabled && source.PublicDeployEnabled, $"{slug} is no longer an enabled, cleared source — remove it from this test or fix the row");

        var companies = source.Tier == SourceTier.A
            ? await db.TargetCompanies.AsNoTracking()
                .Where(t => t.SourceId == source.Id && t.Status != TargetCompanyStatus.Rejected)
                .ToListAsync()
            : [];

        var postings = await Adapter(source.AdapterType)
            .FetchAsync(new JobSourceContext(source, companies), CancellationToken.None)
            .ToListAsync();

        Assert.NotEmpty(postings);
        Assert.All(postings, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Vacancy.ExternalId));
            Assert.False(string.IsNullOrWhiteSpace(p.Vacancy.Title));
            Assert.True(Uri.IsWellFormedUriString(p.Vacancy.Url, UriKind.Absolute), $"url not absolute: {p.Vacancy.Url}");
            Assert.False(string.IsNullOrWhiteSpace(p.Payload));
        });

        // A Tier A source is only as alive as its board tokens. The adapter
        // skips a dead board with a warning and carries on, so a rotated token
        // would pass the assertions above on the strength of the other
        // companies. A board that answers 200 with no openings is fine — a
        // watch-status company is exactly that — so silence is checked against
        // the endpoint directly rather than counted as a failure.
        if (source.Tier == SourceTier.A)
        {
            var silent = companies.Where(c => !postings.Any(p => p.Vacancy.Company == c.CompanyName));

            foreach (var company in silent)
            {
                using var client = Http.CreateClient(IngestHttp.ClientName);
                using var response = await client.GetAsync(BoardUrl(source, company.BoardToken));

                Assert.True(
                    response.IsSuccessStatusCode,
                    $"{slug} board {company.BoardToken} ({company.CompanyName}) answered {(int)response.StatusCode}: the token has rotated or the board is gone");
            }
        }
    }

    /// <summary>
    /// The board URL each Tier A adapter builds, restated here on purpose: if
    /// an adapter's URL drifts from the documented endpoint, this is the test
    /// that should notice.
    /// </summary>
    private static string BoardUrl(Source source, string token) => source.AdapterType switch
    {
        "greenhouse" => $"{source.BaseUrl!.TrimEnd('/')}/v1/boards/{Uri.EscapeDataString(token)}/jobs?content=true",
        "lever" => $"{source.BaseUrl!.TrimEnd('/')}/v0/postings/{Uri.EscapeDataString(token)}?mode=json",
        _ => throw new InvalidOperationException($"no board URL for '{source.AdapterType}'"),
    };

    [Fact]
    public async Task Jobicy_still_states_its_display_terms_in_the_feed()
    {
        // The clearance verdict in spikes/jobicy/NOTES.md rests on the
        // friendlyNotice Jobicy embeds in every response. If it disappears or
        // changes, the terms have moved and A-004 needs re-reading.
        using var client = Http.CreateClient(IngestHttp.ClientName);
        using var document = await IngestHttp.GetJsonAsync(client, "https://jobicy.com/api/v2/remote-jobs?count=1", CancellationToken.None);

        var notice = document.RootElement.String("friendlyNotice");

        Assert.NotNull(notice);
        Assert.Contains("credited", notice);
        Assert.Contains("original job URL", notice);
    }
}
