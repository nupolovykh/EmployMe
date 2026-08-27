using Api.Data;
using Api.HhRu.Dtos;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.HhRu;

public record HhRuIngestResult(int Inserted, int Updated);

public class HhRuIngestService(AppDbContext db, IHhRuVacancyClient client)
{
    public async Task<HhRuIngestResult> IngestAsync(
        HhRuVacancySearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var source = await db.Sources.FirstOrDefaultAsync(s => s.Slug == "hh.ru", cancellationToken);
        if (source is null)
        {
            source = new Source
            {
                Slug = "hh.ru",
                DisplayName = "hh.ru",
                Tier = SourceTier.D,
                AdapterType = "hh_ru",
                BaseUrl = "https://hh.ru",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Sources.Add(source);
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingByExternalId = await db.Vacancies
            .Where(v => v.SourceId == source.Id)
            .ToDictionaryAsync(v => v.ExternalId, cancellationToken);

        var inserted = 0;
        var updated = 0;

        await foreach (var item in client.SearchAllAsync(query, cancellationToken))
        {
            if (existingByExternalId.TryGetValue(item.Id, out var existing))
            {
                ApplyUpdate(existing, item);
                updated++;
            }
            else
            {
                db.Vacancies.Add(item.ToVacancy(source.Id));
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new HhRuIngestResult(inserted, updated);
    }

    private static void ApplyUpdate(Vacancy existing, HhRuVacancyItem item)
    {
        var mapped = item.ToVacancy(existing.SourceId);
        existing.Title = mapped.Title;
        existing.Company = mapped.Company;
        existing.Url = mapped.Url;
        existing.Location = mapped.Location;
        existing.WorkFormat = mapped.WorkFormat;
        existing.Description = mapped.Description;
        existing.SalaryMin = mapped.SalaryMin;
        existing.SalaryMax = mapped.SalaryMax;
        existing.Currency = mapped.Currency;
        existing.PublishedAt = mapped.PublishedAt;
        existing.FetchedAt = mapped.FetchedAt;
    }
}
