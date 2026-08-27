using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Api.Ingest.Adapters;

/// <summary>
/// Tier B. <c>GET /api/v2/remote-jobs</c>. Shape verified in spikes/jobicy/
/// (EM-48). Display conditions are binding: Jobicy stays named as the source and
/// the apply link must point at the original Jobicy URL, which the <c>url</c>
/// field supplies directly — so it is mapped straight through, never rewritten.
/// </summary>
public sealed class JobicyJobSource(IHttpClientFactory httpClientFactory) : IJobSource
{
    private const int MaxCount = 100;

    public string AdapterType => "jobicy";

    public async IAsyncEnumerable<FetchedPosting> FetchAsync(
        JobSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(IngestHttp.ClientName);
        var baseUrl = (context.Source.BaseUrl ?? "https://jobicy.com").TrimEnd('/');

        using var document = await IngestHttp.GetJsonAsync(
            client, $"{baseUrl}/api/v2/remote-jobs?count={MaxCount}", cancellationToken);

        if (!document.RootElement.TryGetProperty("jobs", out var jobs)
            || jobs.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var job in jobs.EnumerateArray())
        {
            if (Map(job) is { } posting)
            {
                yield return posting;
            }
        }
    }

    private static FetchedPosting? Map(JsonElement job)
    {
        var externalId = job.Identifier("id");
        var title = job.String("jobTitle");
        var url = job.String("url");

        if (externalId is null || title is null || url is null)
        {
            return null;
        }

        // The model stores a single salary range with no period, so mixing an
        // hourly figure into it would make the column meaningless. Only annual
        // figures are carried over.
        var yearly = string.Equals(job.String("salaryPeriod"), "yearly", StringComparison.OrdinalIgnoreCase);

        var vacancy = new NormalizedVacancy
        {
            ExternalId = externalId,
            Title = title,
            Url = url,
            Company = job.String("companyName"),
            Location = job.String("jobGeo"),
            // Every posting on this endpoint is a remote role by construction.
            WorkFormat = "remote",
            Description = HtmlText.ToPlainText(job.String("jobDescription") ?? job.String("jobExcerpt")),
            SalaryMin = yearly ? job.Int("salaryMin") : null,
            SalaryMax = yearly ? job.Int("salaryMax") : null,
            Currency = yearly ? job.String("salaryCurrency") : null,
            PublishedAt = job.Timestamp("pubDate"),
        };

        return new FetchedPosting(vacancy, job.GetRawText());
    }
}
