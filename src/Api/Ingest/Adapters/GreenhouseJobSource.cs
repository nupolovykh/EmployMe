using System.Runtime.CompilerServices;
using System.Text.Json;
using Api.Models;

namespace Api.Ingest.Adapters;

/// <summary>
/// Tier A. <c>GET /v1/boards/{board_token}/jobs?content=true</c>, one call per
/// target company — the board API has no search, so the registry is the query.
/// Shape verified in spikes/greenhouse/ (EM-45).
/// </summary>
public sealed class GreenhouseJobSource(IHttpClientFactory httpClientFactory) : IJobSource
{
    public string AdapterType => "greenhouse";

    public async IAsyncEnumerable<FetchedPosting> FetchAsync(
        JobSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(IngestHttp.ClientName);
        var baseUrl = (context.Source.BaseUrl ?? "https://boards-api.greenhouse.io").TrimEnd('/');

        foreach (var company in context.TargetCompanies)
        {
            var url = $"{baseUrl}/v1/boards/{Uri.EscapeDataString(company.BoardToken)}/jobs?content=true";
            using var document = await IngestHttp.GetJsonAsync(client, url, cancellationToken);

            if (!document.RootElement.TryGetProperty("jobs", out var jobs)
                || jobs.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var job in jobs.EnumerateArray())
            {
                if (Map(job, company) is { } posting)
                {
                    yield return posting;
                }
            }
        }
    }

    private static FetchedPosting? Map(JsonElement job, TargetCompany company)
    {
        var externalId = job.Identifier("id");
        var title = job.String("title");
        var url = job.String("absolute_url");

        if (externalId is null || title is null || url is null)
        {
            return null;
        }

        var location = job.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object
            ? loc.String("name")
            : null;

        var vacancy = new NormalizedVacancy
        {
            ExternalId = externalId,
            Title = title,
            Url = url,
            // company_name is present on GitLab's board but is not guaranteed
            // across boards; the registry row is the reliable fallback.
            Company = job.String("company_name") ?? company.CompanyName,
            Location = location,
            Description = HtmlText.ToPlainText(job.String("content")),
            PublishedAt = job.Timestamp("first_published") ?? job.Timestamp("updated_at"),
        };

        return new FetchedPosting(vacancy, job.GetRawText());
    }
}
