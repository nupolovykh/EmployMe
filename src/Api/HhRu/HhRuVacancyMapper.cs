using Api.HhRu.Dtos;
using Api.Models;

namespace Api.HhRu;

public static class HhRuVacancyMapper
{
    public static Vacancy ToVacancy(this HhRuVacancyItem item, int sourceId)
    {
        var workFormat = item.WorkFormat is { Count: > 0 }
            ? string.Join(", ", item.WorkFormat
                .Select(w => w.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name)))
            : item.Schedule?.Name;

        var description = string.Join(
            "\n\n",
            new[] { item.Snippet?.Requirement, item.Snippet?.Responsibility }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        return new Vacancy
        {
            SourceId = sourceId,
            ExternalId = item.Id,
            Title = item.Name,
            Company = item.Employer?.Name,
            Url = item.AlternateUrl,
            Location = item.Area?.Name,
            WorkFormat = string.IsNullOrWhiteSpace(workFormat) ? null : workFormat,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            SalaryMin = item.Salary?.From,
            SalaryMax = item.Salary?.To,
            Currency = item.Salary?.Currency,
            PublishedAt = item.PublishedAt,
            FetchedAt = DateTimeOffset.UtcNow,
        };
    }
}
