namespace Api.Models.Dtos;

public record VacancyDto(
    int Id,
    string ExternalId,
    string Title,
    string? Company,
    string Url,
    string? Location,
    string? WorkFormat,
    int? SalaryMin,
    int? SalaryMax,
    string? Currency,
    DateTimeOffset? PublishedAt,
    DateTimeOffset FetchedAt,
    Seniority Seniority,
    string SourceName,
    string SourceSlug,
    string? SourceUrl,
    bool AttributionRequired);
