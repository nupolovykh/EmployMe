using Api.Data;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VacanciesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<VacancyDto>>> GetVacancies(
        string? keyword = null,
        string? location = null,
        DateTimeOffset? publishedAfter = null,
        DateTimeOffset? publishedBefore = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Vacancies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // No structured tech-stack field exists yet (that's Phase III's LLM
            // extraction, EM-31) — free-text search over title/company/description
            // is what "filter by stack" reduces to for now.
            var pattern = $"%{keyword}%";
            query = query.Where(v =>
                EF.Functions.ILike(v.Title, pattern) ||
                (v.Company != null && EF.Functions.ILike(v.Company, pattern)) ||
                (v.Description != null && EF.Functions.ILike(v.Description, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var pattern = $"%{location}%";
            query = query.Where(v => v.Location != null && EF.Functions.ILike(v.Location, pattern));
        }

        if (publishedAfter is not null)
        {
            query = query.Where(v => v.PublishedAt >= publishedAfter);
        }

        if (publishedBefore is not null)
        {
            query = query.Where(v => v.PublishedAt <= publishedBefore);
        }

        var vacancies = await query
            .OrderByDescending(v => v.PublishedAt ?? v.FetchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VacancyDto(
                v.Id,
                v.ExternalId,
                v.Title,
                v.Company,
                v.Url,
                v.Location,
                v.WorkFormat,
                v.SalaryMin,
                v.SalaryMax,
                v.Currency,
                v.PublishedAt,
                v.FetchedAt,
                v.Source!.DisplayName))
            .ToListAsync(cancellationToken);

        return Ok(vacancies);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VacancyDto>> GetVacancy(int id, CancellationToken cancellationToken)
    {
        var vacancy = await db.Vacancies
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VacancyDto(
                v.Id,
                v.ExternalId,
                v.Title,
                v.Company,
                v.Url,
                v.Location,
                v.WorkFormat,
                v.SalaryMin,
                v.SalaryMax,
                v.Currency,
                v.PublishedAt,
                v.FetchedAt,
                v.Source!.DisplayName))
            .FirstOrDefaultAsync(cancellationToken);

        return vacancy is null ? NotFound() : Ok(vacancy);
    }
}
