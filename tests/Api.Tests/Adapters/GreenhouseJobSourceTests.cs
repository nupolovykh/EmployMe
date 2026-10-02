using System.Net;
using Api.Ingest;
using Api.Ingest.Adapters;
using Api.Models;
using Api.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Adapters;

public class GreenhouseJobSourceTests
{
    private const string Board = "https://boards-api.greenhouse.io/v1/boards/gitlab/jobs?content=true";

    private static JobSourceContext Context(params TargetCompany[] companies) =>
        new(TestSources.Source("greenhouse", SourceTier.A), companies);

    [Fact]
    public async Task Maps_every_job_in_the_spike_response()
    {
        var http = new StubHttpClientFactory().Respond(Board, Spikes.Read("greenhouse"));
        var adapter = new GreenhouseJobSource(http, NullLogger<GreenhouseJobSource>.Instance);

        var postings = await adapter.FetchAsync(Context(TestSources.Company("GitLab", "gitlab")), CancellationToken.None).ToListAsync();

        Assert.Equal(3, postings.Count);

        var first = postings[0].Vacancy;
        Assert.Equal("8503792002", first.ExternalId);
        Assert.Equal("https://job-boards.greenhouse.io/gitlab/jobs/8503792002", first.Url);
        Assert.Equal("Remote, Italy", first.Location);
        Assert.NotNull(first.Title);
        Assert.NotNull(first.Description);
        Assert.DoesNotContain("<", first.Description);
    }

    [Fact]
    public async Task Timestamps_are_normalized_to_utc()
    {
        // first_published arrives in the board's local offset (-04:00 in the
        // spike); Npgsql rejects a non-zero offset on timestamptz.
        var http = new StubHttpClientFactory().Respond(Board, Spikes.Read("greenhouse"));
        var adapter = new GreenhouseJobSource(http, NullLogger<GreenhouseJobSource>.Instance);

        var postings = await adapter.FetchAsync(Context(TestSources.Company("GitLab", "gitlab")), CancellationToken.None).ToListAsync();

        Assert.All(postings, p => Assert.Equal(TimeSpan.Zero, p.Vacancy.PublishedAt!.Value.Offset));
        Assert.Equal(new DateTimeOffset(2026, 4, 17, 9, 58, 3, TimeSpan.Zero), postings[0].Vacancy.PublishedAt);
    }

    [Fact]
    public async Task A_dead_board_token_does_not_cost_the_other_companies()
    {
        var http = new StubHttpClientFactory()
            .Respond("https://boards-api.greenhouse.io/v1/boards/gone/jobs?content=true", "", HttpStatusCode.NotFound)
            .Respond(Board, Spikes.Read("greenhouse"));
        var adapter = new GreenhouseJobSource(http, NullLogger<GreenhouseJobSource>.Instance);

        var postings = await adapter.FetchAsync(
            Context(TestSources.Company("Gone", "gone"), TestSources.Company("GitLab", "gitlab")),
            CancellationToken.None).ToListAsync();

        Assert.Equal(3, postings.Count);
        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task Company_falls_back_to_the_registry_row()
    {
        var http = new StubHttpClientFactory().Respond(Board, """{"jobs":[{"id":1,"title":"Dev","absolute_url":"https://x/1"}]}""");
        var adapter = new GreenhouseJobSource(http, NullLogger<GreenhouseJobSource>.Instance);

        var postings = await adapter.FetchAsync(Context(TestSources.Company("Registry Name", "gitlab")), CancellationToken.None).ToListAsync();

        Assert.Equal("Registry Name", Assert.Single(postings).Vacancy.Company);
    }
}
