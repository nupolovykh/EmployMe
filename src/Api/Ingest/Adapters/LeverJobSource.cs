using System.Runtime.CompilerServices;
using System.Text.Json;
using Api.Models;

namespace Api.Ingest.Adapters;

/// <summary>
/// Tier A. <c>GET /v0/postings/{site}?mode=json</c>, one call per target
/// company. The root is a flat JSON array, not an object — the <c>postings</c>
/// key in spikes/lever/response.json is only that file's truncation wrapper
/// (EM-46 NOTES.md).
/// </summary>
public sealed class LeverJobSource(IHttpClientFactory httpClientFactory) : IJobSource
{
    public string AdapterType => "lever";

    public async IAsyncEnumerable<FetchedPosting> FetchAsync(
        JobSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(IngestHttp.ClientName);
        var baseUrl = (context.Source.BaseUrl ?? "https://api.lever.co").TrimEnd('/');

        foreach (var company in context.TargetCompanies)
        {
            var url = $"{baseUrl}/v0/postings/{Uri.EscapeDataString(company.BoardToken)}?mode=json";
            using var document = await IngestHttp.GetJsonAsync(client, url, cancellationToken);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var posting in document.RootElement.EnumerateArray())
            {
                if (Map(posting, company) is { } mapped)
                {
                    yield return mapped;
                }
            }
        }
    }

    private static FetchedPosting? Map(JsonElement posting, TargetCompany company)
    {
        var externalId = posting.Identifier("id");
        var title = posting.String("text");
        var url = posting.String("hostedUrl");

        if (externalId is null || title is null || url is null)
        {
            return null;
        }

        var location = posting.TryGetProperty("categories", out var categories)
            && categories.ValueKind == JsonValueKind.Object
                ? categories.String("location")
                : null;

        var vacancy = new NormalizedVacancy
        {
            ExternalId = externalId,
            Title = title,
            Url = url,
            // Lever's payload never names the employer — the site token does.
            Company = company.CompanyName,
            Location = location,
            WorkFormat = posting.String("workplaceType"),
            // descriptionPlain is Lever's own plain-text rendering; prefer it to
            // stripping the HTML ourselves.
            Description = posting.String("descriptionPlain") is { Length: > 0 } plain
                ? HtmlText.ToPlainText(plain)
                : HtmlText.ToPlainText(posting.String("description")),
            PublishedAt = posting.Long("createdAt") is { } createdAt
                ? DateTimeOffset.FromUnixTimeMilliseconds(createdAt)
                : null,
        };

        return new FetchedPosting(vacancy, posting.GetRawText());
    }
}
