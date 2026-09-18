using System.Net;
using Api.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Api.Tests.Ingest;

/// <summary>
/// Exercises the real pipeline from <see cref="IngestResilience.Configure"/>
/// through the real HttpClientFactory, with only the retry delay shortened so
/// the test does not wait out the exponential backoff.
/// </summary>
public class IngestResilienceTests
{
    private static (HttpClient Client, List<HttpStatusCode> Sent) Build(params HttpStatusCode[] answers)
    {
        var queue = new Queue<HttpStatusCode>(answers);
        var sent = new List<HttpStatusCode>();
        var services = new ServiceCollection();

        services.AddHttpClient("ingest")
            .ConfigurePrimaryHttpMessageHandler(() => new ScriptedHandler(queue, sent))
            .AddStandardResilienceHandler(options =>
            {
                IngestResilience.Configure(options);
                options.Retry.Delay = TimeSpan.FromMilliseconds(10);
                options.Retry.MaxDelay = TimeSpan.FromMilliseconds(50);
            })
            .SelectPipelineByAuthority();

        var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient("ingest");
        return (client, sent);
    }

    [Fact]
    public async Task Transient_5xx_is_retried_until_it_succeeds()
    {
        var (client, sent) = Build(HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway, HttpStatusCode.OK);

        var response = await client.GetAsync("https://example.test/jobs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, sent.Count);
    }

    [Fact]
    public async Task Too_many_requests_is_retried()
    {
        var (client, sent) = Build(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);

        var response = await client.GetAsync("https://example.test/jobs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, sent.Count);
    }

    [Fact]
    public async Task Not_found_is_not_retried()
    {
        // A dead Tier A board token is a fact, not a hiccup; retrying it three
        // times would only delay the per-company skip in the adapters.
        var (client, sent) = Build(HttpStatusCode.NotFound, HttpStatusCode.OK);

        var response = await client.GetAsync("https://example.test/jobs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Single(sent);
    }

    [Fact]
    public async Task Gives_up_after_the_configured_attempts()
    {
        var (client, sent) = Build(
            HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

        var response = await client.GetAsync("https://example.test/jobs");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(4, sent.Count);
    }

    private sealed class ScriptedHandler(Queue<HttpStatusCode> answers, List<HttpStatusCode> sent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var status = answers.Count > 0 ? answers.Dequeue() : HttpStatusCode.OK;
            sent.Add(status);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{}") });
        }
    }
}
