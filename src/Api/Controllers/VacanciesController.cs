using System.Linq.Expressions;
using Api.Data;
using Api.Models;
using Api.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VacanciesController(AppDbContext db) : ControllerBase
{
    private const string LikeEscape = "\\";

    // EM-54: every card credits its source, so the source columns travel with
    // every projection rather than being bolted onto one endpoint.
    private static readonly Expression<Func<Vacancy, VacancyDto>> VacancyProjection =
        v => new VacancyDto(
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
            v.Seniority,
            v.Source!.DisplayName,
            v.Source!.Slug,
            v.Source!.BaseUrl,
            v.Source!.AttributionRequired);

    [HttpGet]
    public async Task<ActionResult<PagedResult<VacancyDto>>> GetVacancies(
        string? keyword = null,
        string? location = null,
        DateTimeOffset? publishedAfter = null,
        DateTimeOffset? publishedBefore = null,
        Seniority? seniority = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Clamped at both ends. Only the lower bound was checked, so a large page
        // number overflowed (page - 1) * pageSize into a negative offset, which
        // Postgres rejects — an unauthenticated 500 from a query string.
        //
        // No `+ 1` on the upper bound: at pageSize 1 that is int.MaxValue + 1,
        // which wraps negative and makes Clamp itself throw with max below min —
        // trading one 500 for another. The bound as written is the largest page
        // whose offset still fits.
        page = Math.Clamp(page, 1, int.MaxValue / pageSize);

        var query = db.Vacancies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // No structured tech-stack field exists yet (that's Phase III's LLM
            // extraction, EM-31) — free-text search over title/company/description
            // is what "filter by stack" reduces to for now.
            var pattern = $"%{EscapeLike(keyword)}%";
            query = query.Where(v =>
                EF.Functions.ILike(v.Title, pattern, LikeEscape) ||
                (v.Company != null && EF.Functions.ILike(v.Company, pattern, LikeEscape)) ||
                (v.Description != null && EF.Functions.ILike(v.Description, pattern, LikeEscape)));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var pattern = $"%{EscapeLike(location)}%";
            query = query.Where(v =>
                v.Location != null && EF.Functions.ILike(v.Location, pattern, LikeEscape));
        }

        // Compared against the same fallback the ordering uses. Filtering on
        // PublishedAt alone silently dropped every posting without one — all of
        // Greenhouse's and Lever's — because NULL >= x is NULL in SQL, so touching
        // the date filter made two whole sources disappear with no indication.
        if (publishedAfter is not null)
        {
            query = query.Where(v => (v.PublishedAt ?? v.FetchedAt) >= publishedAfter);
        }

        if (publishedBefore is not null)
        {
            query = query.Where(v => (v.PublishedAt ?? v.FetchedAt) <= publishedBefore);
        }

        // Filtering for a level excludes Unknown rather than including it.
        // Most rows are Unknown — Greenhouse and Lever state no level at all
        // until EM-31 can infer one — and treating "not stated" as a match
        // would make the filter useless in exactly the case it exists for.
        if (seniority is not null)
        {
            query = query.Where(v => v.Seniority == seniority);
        }

        // Counted after the filters are applied: a total describing the whole
        // table would have the client offering pages the filter cannot fill.
        var total = await query.CountAsync(cancellationToken);

        var vacancies = await query
            .OrderByDescending(v => v.PublishedAt ?? v.FetchedAt)
            // Id breaks ties. The sort key is not unique — Jobicy publishes to the
            // day and Arbeitnow to the second, so dozens of rows share one — and
            // Postgres gives no stable order among equals across separate
            // LIMIT/OFFSET queries, which makes a row appear on two pages while
            // another is never returned at all.
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(VacancyProjection)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<VacancyDto>(vacancies, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VacancyDto>> GetVacancy(int id, CancellationToken cancellationToken)
    {
        var vacancy = await db.Vacancies
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(VacancyProjection)
            .FirstOrDefaultAsync(cancellationToken);

        return vacancy is null ? NotFound() : Ok(vacancy);
    }

    /// <summary>
    /// ILIKE reads % and _ as wildcards, so an unescaped keyword silently widens
    /// the search — "100%" would match every row rather than the literal string.
    /// </summary>
    private static string EscapeLike(string input) => input
        .Replace(LikeEscape, LikeEscape + LikeEscape)
        .Replace("%", LikeEscape + "%")
        .Replace("_", LikeEscape + "_");
}
