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

        if (publishedAfter is not null)
        {
            query = query.Where(v => v.PublishedAt >= publishedAfter);
        }

        if (publishedBefore is not null)
        {
            query = query.Where(v => v.PublishedAt <= publishedBefore);
        }

        // Counted after the filters are applied: a total describing the whole
        // table would have the client offering pages the filter cannot fill.
        var total = await query.CountAsync(cancellationToken);

        var vacancies = await query
            .OrderByDescending(v => v.PublishedAt ?? v.FetchedAt)
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
