using System.Net;
using System.Text;

namespace Api.Tests.Support;

/// <summary>
/// An IHttpClientFactory whose client answers from a table of canned responses,
/// keyed by absolute URL. Anything not in the table is a 404, which is also
/// what a dead Tier A board token answers with.
/// </summary>
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new(StringComparer.Ordinal);

    public List<string> Requests { get; } = [];

    public StubHttpClientFactory Respond(string url, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses[url] = (status, body);
        return this;
    }

    public HttpClient CreateClient(string name) => new(new Handler(this));

    private sealed class Handler(StubHttpClientFactory owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            owner.Requests.Add(url);

            var (status, body) = owner._responses.TryGetValue(url, out var canned)
                ? canned
                : (HttpStatusCode.NotFound, "{}");

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
