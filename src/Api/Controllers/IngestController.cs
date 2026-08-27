using Api.HhRu;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestController(HhRuIngestService ingestService) : ControllerBase
{
    [HttpPost("hh-ru")]
    public async Task<ActionResult<HhRuIngestResult>> IngestHhRu(
        [FromQuery] string? text,
        [FromQuery] string? area,
        CancellationToken cancellationToken)
    {
        var query = new HhRuVacancySearchQuery(Text: text, Area: area);
        var result = await ingestService.IngestAsync(query, cancellationToken);
        return Ok(result);
    }
}
