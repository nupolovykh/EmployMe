using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Api.HhRu.Dtos;
using Microsoft.AspNetCore.WebUtilities;

namespace Api.HhRu;

public class HhRuVacancyClient(HttpClient httpClient) : IHhRuVacancyClient
{
    // hh.ru refuses pagination past this depth regardless of per_page (see docs/vacancies.md).
    private const int MaxResultDepth = 2000;

    public async Task<HhRuVacancySearchResponse> SearchAsync(
        HhRuVacancySearchQuery query,
        int page,
        CancellationToken cancellationToken = default)
    {
        var url = BuildSearchUrl(query, page);
        var response = await httpClient.GetFromJsonAsync<HhRuVacancySearchResponse>(url, cancellationToken);
        return response ?? throw new InvalidOperationException("hh.ru returned an empty response body for vacancy search.");
    }

    public async IAsyncEnumerable<HhRuVacancyItem> SearchAllAsync(
        HhRuVacancySearchQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = 0;
        while ((long)(page + 1) * query.PerPage <= MaxResultDepth)
        {
            var response = await SearchAsync(query, page, cancellationToken);

            foreach (var item in response.Items)
            {
                yield return item;
            }

            page++;
            if (page >= response.Pages || response.Items.Count == 0)
            {
                yield break;
            }
        }
    }

    private static string BuildSearchUrl(HhRuVacancySearchQuery query, int page)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["per_page"] = query.PerPage.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            parameters["text"] = query.Text;
        }

        if (!string.IsNullOrWhiteSpace(query.Area))
        {
            parameters["area"] = query.Area;
        }

        if (query.DateFrom is not null)
        {
            parameters["date_from"] = query.DateFrom.Value.ToString("yyyy-MM-ddTHH:mm:sszzz");
        }

        if (query.DateTo is not null)
        {
            parameters["date_to"] = query.DateTo.Value.ToString("yyyy-MM-ddTHH:mm:sszzz");
        }

        return QueryHelpers.AddQueryString("vacancies", parameters);
    }
}
