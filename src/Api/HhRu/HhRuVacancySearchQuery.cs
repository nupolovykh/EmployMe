namespace Api.HhRu;

public record HhRuVacancySearchQuery(
    string? Text = null,
    string? Area = null,
    int PerPage = 50,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null);
