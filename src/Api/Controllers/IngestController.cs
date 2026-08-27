using Api.Ingest;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController(IngestService ingest) : ControllerBase
{
    /// <summary>
    /// Source-agnostic manual ingest (EM-52). Replaces the cancelled hh.ru-specific
    /// job (EM-14). Phase II's scheduler (EM-18) calls the same service.
    /// </summary>
    /// <param name="source">Slug of a single source; all enabled sources when omitted.</param>
    /// <param name="force">Ignore the source's min poll interval. Manual runs only.</param>
    [HttpPost]
    public async Task<ActionResult<IngestReport>> Run(
        string? source = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var report = await ingest.RunAsync(source, force, cancellationToken);

        return report.Sources.Count == 0
            ? NotFound(new { message = $"No source matched '{source}'." })
            : Ok(report);
    }
}
