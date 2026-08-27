using System.Text.Json.Serialization;

namespace Api.HhRu.Dtos;

public record HhRuVacancySearchResponse(
    [property: JsonPropertyName("items")] List<HhRuVacancyItem> Items,
    [property: JsonPropertyName("found")] int Found,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pages")] int Pages,
    [property: JsonPropertyName("per_page")] int PerPage);

public record HhRuVacancyItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("alternate_url")] string AlternateUrl,
    [property: JsonPropertyName("employer")] HhRuEmployer? Employer,
    [property: JsonPropertyName("area")] HhRuArea? Area,
    [property: JsonPropertyName("salary")] HhRuSalary? Salary,
    [property: JsonPropertyName("published_at")] DateTimeOffset PublishedAt,
    [property: JsonPropertyName("schedule")] HhRuNamedRef? Schedule,
    [property: JsonPropertyName("work_format")] List<HhRuNamedRef>? WorkFormat,
    [property: JsonPropertyName("snippet")] HhRuSnippet? Snippet);

public record HhRuEmployer([property: JsonPropertyName("name")] string? Name);

public record HhRuArea([property: JsonPropertyName("name")] string? Name);

public record HhRuSalary(
    [property: JsonPropertyName("from")] int? From,
    [property: JsonPropertyName("to")] int? To,
    [property: JsonPropertyName("currency")] string? Currency);

public record HhRuNamedRef([property: JsonPropertyName("name")] string? Name);

public record HhRuSnippet(
    [property: JsonPropertyName("requirement")] string? Requirement,
    [property: JsonPropertyName("responsibility")] string? Responsibility);
