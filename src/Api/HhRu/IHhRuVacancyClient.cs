using Api.HhRu.Dtos;

namespace Api.HhRu;

public interface IHhRuVacancyClient
{
    Task<HhRuVacancySearchResponse> SearchAsync(
        HhRuVacancySearchQuery query,
        int page,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<HhRuVacancyItem> SearchAllAsync(
        HhRuVacancySearchQuery query,
        CancellationToken cancellationToken = default);
}
