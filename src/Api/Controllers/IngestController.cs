using System.Security.Cryptography;
using System.Text;
using Api.Ingest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController(IngestService ingest, IOptions<IngestOptions> options) : ControllerBase
{
    private const string TokenHeader = "X-Ingest-Token";

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
        if (Authorize() is { } failure)
        {
            return failure;
        }

        var report = await ingest.RunAsync(source, force, cancellationToken);

        return report.Sources.Count == 0
            ? NotFound(new { message = $"No source matched '{source}'." })
            : Ok(report);
    }

    /// <summary>
    /// Open on a development machine, shared-secret on anything the public can
    /// reach. <c>force=true</c> skips <c>min_poll_interval</c>, so an anonymous
    /// caller looping this endpoint would breach Jobicy's one-per-hour cap for
    /// us — the ban would be ours, not theirs.
    /// </summary>
    private ObjectResult? Authorize()
    {
        if (options.Value.PublicDeployment is false)
        {
            return null;
        }

        var expected = options.Value.TriggerToken;

        if (string.IsNullOrEmpty(expected))
        {
            // Refusing is the safe failure: running unauthenticated is what
            // risks the sources we spent Phase 0 qualifying.
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Ingest disabled",
                detail: $"{IngestOptions.SectionName}:{nameof(IngestOptions.TriggerToken)} is not "
                      + "configured, and this environment is treated as publicly reachable.");
        }

        var supplied = Request.Headers[TokenHeader].ToString();

        return FixedTimeEquals(supplied, expected)
            ? null
            : Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid ingest token");
    }

    private static bool FixedTimeEquals(string supplied, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
}
