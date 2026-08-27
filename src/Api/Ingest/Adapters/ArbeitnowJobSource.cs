using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Api.Ingest.Adapters;

/// <summary>
/// Tier B, EU/DACH. <c>GET /api/job-board-api</c>, Laravel-style pagination
/// followed via <c>links.next</c>. Shape verified in spikes/arbeitnow/ (EM-49):
/// <c>slug</c> is the external id — there is no numeric id field.
/// </summary>
public sealed class ArbeitnowJobSource(
    IHttpClientFactory httpClientFactory,
    IOptions<IngestOptions> options) : IJobSource
{
    public string AdapterType => "arbeitnow";

    public async IAsyncEnumerable<FetchedPosting> FetchAsync(
        JobSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(IngestHttp.ClientName);
        var baseUrl = (context.Source.BaseUrl ?? "https://www.arbeitnow.com").TrimEnd('/');
        var url = $"{baseUrl}/api/job-board-api";

        for (var page = 0; page < options.Value.MaxPagesPerSource && url is not null; page++)
        {
            using var document = await IngestHttp.GetJsonAsync(client, url, cancellationToken);

            if (!document.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var job in data.EnumerateArray())
            {
                if (Map(job) is { } posting)
                {
                    yield return posting;
                }
            }

            url = document.RootElement.TryGetProperty("links", out var links)
                && links.ValueKind == JsonValueKind.Object
                    ? links.String("next")
                    : null;
        }
    }

    private static FetchedPosting? Map(JsonElement job)
    {
        var externalId = job.String("slug");
        var title = job.String("title");
        var url = job.String("url");

        if (externalId is null || title is null || url is null)
        {
            return null;
        }

        var isRemote = job.TryGetProperty("remote", out var remote)
            && remote.ValueKind == JsonValueKind.True;

        var vacancy = new NormalizedVacancy
        {
            ExternalId = externalId,
            Title = title,
            Url = url,
            Company = job.String("company_name"),
            Location = job.String("location"),
            // `remote: false` only means the flag is absent, not that the role is
            // known to be onsite.
            WorkFormat = isRemote ? "remote" : null,
            Description = HtmlText.ToPlainText(job.String("description")),
            PublishedAt = job.Long("created_at") is { } createdAt
                ? DateTimeOffset.FromUnixTimeSeconds(createdAt)
                : null,
        };

        return new FetchedPosting(vacancy, job.GetRawText());
    }
}
