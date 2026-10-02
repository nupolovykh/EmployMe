using Api.Ingest;
using Api.Ingest.Adapters;
using Api.Models;
using Api.Tests.Support;
using Microsoft.Extensions.Options;

namespace Api.Tests.Adapters;

public class ArbeitnowJobSourceTests
{
    private const string Page1 = "https://www.arbeitnow.com/api/job-board-api";
    private const string Page2 = "https://www.arbeitnow.com/api/job-board-api?page=2";

    private static JobSourceContext Context() => new(TestSources.Source("arbeitnow"), []);

    private static ArbeitnowJobSource Adapter(StubHttpClientFactory http, int maxPages = 5) =>
        new(http, Options.Create(new IngestOptions { MaxPagesPerSource = maxPages }));

    [Fact]
    public async Task Maps_the_spike_page_with_slug_as_external_id()
    {
        var http = new StubHttpClientFactory().Respond(Page1, Spikes.Read("arbeitnow"));

        var postings = await Adapter(http, maxPages: 1).FetchAsync(Context(), CancellationToken.None).ToListAsync();

        Assert.Equal(3, postings.Count);
        var first = postings[0].Vacancy;
        Assert.StartsWith("anlagentechniker", first.ExternalId);
        Assert.Equal(Seniority.Senior, first.Seniority);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1787742059), first.PublishedAt);
        Assert.DoesNotContain("<p>", first.Description);
    }

    [Fact]
    public async Task Follows_links_next_up_to_the_page_bound()
    {
        var http = new StubHttpClientFactory()
            .Respond(Page1, Spikes.Read("arbeitnow"))
            .Respond(Page2, Spikes.Read("arbeitnow"));

        var postings = await Adapter(http, maxPages: 2).FetchAsync(Context(), CancellationToken.None).ToListAsync();

        Assert.Equal(6, postings.Count);
        Assert.Equal([Page1, Page2], http.Requests);
    }

    [Fact]
    public async Task A_next_link_on_another_origin_is_not_followed()
    {
        var page = Spikes.Read("arbeitnow").Replace(Page2, "https://evil.example/api/job-board-api?page=2");
        var http = new StubHttpClientFactory().Respond(Page1, page);

        var postings = await Adapter(http).FetchAsync(Context(), CancellationToken.None).ToListAsync();

        Assert.Equal(3, postings.Count);
        Assert.Equal([Page1], http.Requests);
    }
}
