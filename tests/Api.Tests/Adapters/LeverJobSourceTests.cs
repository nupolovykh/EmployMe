using Api.Ingest;
using Api.Ingest.Adapters;
using Api.Models;
using Api.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Adapters;

public class LeverJobSourceTests
{
    private static JobSourceContext Context(params TargetCompany[] companies) =>
        new(TestSources.Source("lever", SourceTier.A), companies);

    [Fact]
    public async Task Maps_the_flat_array_the_live_endpoint_returns()
    {
        var http = new StubHttpClientFactory()
            .Respond("https://api.lever.co/v0/postings/qonto?mode=json", Spikes.LeverPostings("qonto"))
            .Respond("https://api.lever.co/v0/postings/peerspace?mode=json", Spikes.LeverPostings("peerspace"));
        var adapter = new LeverJobSource(http, NullLogger<LeverJobSource>.Instance);

        var postings = await adapter.FetchAsync(
            Context(TestSources.Company("Qonto", "qonto"), TestSources.Company("Peerspace", "peerspace")),
            CancellationToken.None).ToListAsync();

        Assert.Equal(4, postings.Count);
        Assert.Equal(2, postings.Count(p => p.Vacancy.Company == "Qonto"));
        Assert.Equal(2, postings.Count(p => p.Vacancy.Company == "Peerspace"));
        Assert.All(postings, p =>
        {
            Assert.NotEmpty(p.Vacancy.ExternalId);
            Assert.StartsWith("https://jobs.lever.co/", p.Vacancy.Url);
            Assert.NotNull(p.Vacancy.PublishedAt);
            Assert.Equal(TimeSpan.Zero, p.Vacancy.PublishedAt!.Value.Offset);
        });
    }

    [Fact]
    public async Task An_object_root_yields_nothing_rather_than_throwing()
    {
        // The spike file's own wrapper shape, which the adapter must not mistake
        // for a posting list.
        var http = new StubHttpClientFactory()
            .Respond("https://api.lever.co/v0/postings/qonto?mode=json", Spikes.Read("lever"));
        var adapter = new LeverJobSource(http, NullLogger<LeverJobSource>.Instance);

        var postings = await adapter.FetchAsync(Context(TestSources.Company("Qonto", "qonto")), CancellationToken.None).ToListAsync();

        Assert.Empty(postings);
    }
}
