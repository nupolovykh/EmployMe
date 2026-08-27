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
    public async Task<ActionResult<PagedResult<VacancyDto>>> GetVacancies(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Vacancies.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);

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

        return Ok(new PagedResult<VacancyDto>(vacancies, total, page, pageSize));
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
