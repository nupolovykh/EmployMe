using Api.Data;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Ingest;

/// <summary>
/// Source-agnostic ingest (EM-52). Knows about <c>sources</c> rows and the
/// adapter registry; knows nothing about any particular upstream. Losing a
/// source is a flipped boolean here, not a rewritten pipeline.
/// </summary>
public sealed class IngestService(
    AppDbContext db,
    IEnumerable<IJobSource> adapters,
    IOptions<IngestOptions> options,
    TimeProvider clock,
    ILogger<IngestService> logger)
{
    private readonly Dictionary<string, IJobSource> _adapters =
        adapters.ToDictionary(a => a.AdapterType, StringComparer.OrdinalIgnoreCase);

    public async Task<IngestReport> RunAsync(
        string? sourceSlug,
        bool force,
        CancellationToken cancellationToken)
    {
        var query = db.Sources.AsQueryable();

        if (!string.IsNullOrWhiteSpace(sourceSlug))
        {
            query = query.Where(s => s.Slug == sourceSlug);
        }

        var sources = await query.OrderBy(s => s.Id).ToListAsync(cancellationToken);
        var results = new List<SourceIngestResult>(sources.Count);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await RunSourceAsync(source, force, cancellationToken));
        }

        return new IngestReport(results);
    }

    private async Task<SourceIngestResult> RunSourceAsync(
        Source source,
        bool force,
        CancellationToken cancellationToken)
    {
        // Unconditional, and checked before Enabled: a Tier D row is disqualified,
        // not merely switched off. See docs/SOURCES.md's disqualified sources.
        if (source.Tier == SourceTier.D)
        {
            return new SourceIngestResult(source.Slug, "skipped", Detail: "tier D is never ingested");
        }

        if (!source.Enabled)
        {
            return new SourceIngestResult(source.Slug, "skipped", Detail: "source is disabled");
        }

        if (options.Value.PublicDeployment is not false && !source.PublicDeployEnabled)
        {
            return new SourceIngestResult(
                source.Slug, "skipped", Detail: "not cleared for display on a public deployment");
        }

        if (!_adapters.TryGetValue(source.AdapterType, out var adapter))
        {
            return new SourceIngestResult(
                source.Slug, "skipped", Detail: $"no adapter registered for '{source.AdapterType}'");
        }

        var now = clock.GetUtcNow();

        // The interval comes off the row. Jobicy caps polling at once per hour and
        // ignoring that gets the project banned.
        if (!force && source.LastSuccessAt is { } last && last + source.MinPollInterval > now)
        {
            var due = last + source.MinPollInterval;
            return new SourceIngestResult(
                source.Slug, "skipped", Detail: $"min poll interval not elapsed; next due {due:u}");
        }

        // Watch rows are still fetched: they are companies worth working for that
        // simply have no fitting role open right now, and noticing when that
        // changes is the point of the registry. Only Rejected is excluded.
        var targets = source.Tier == SourceTier.A
            ? await db.TargetCompanies
                .Where(t => t.SourceId == source.Id && t.Status != TargetCompanyStatus.Rejected)
                .ToListAsync(cancellationToken)
            : [];

        var context = new JobSourceContext(source, targets);

        try
        {
            var (fetched, created, updated) = await PersistAsync(source, adapter, context, cancellationToken);

            logger.LogInformation(
                "Ingested {Source}: {Fetched} fetched, {Created} created, {Updated} updated",
                source.Slug, fetched, created, updated);

            return new SourceIngestResult(source.Slug, "ok", fetched, created, updated);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The failed SaveChanges left this source's postings in the change
            // tracker. Clearing it does two things: the error bookkeeping below
            // can't fail for the same reason the ingest did, and the next source
            // in the loop starts from a clean context instead of retrying these
            // rows. The update is issued as SQL so it needs no tracked entity.
            db.ChangeTracker.Clear();

            var failedAt = clock.GetUtcNow();
            await db.Sources
                .Where(s => s.Id == source.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(s => s.LastErrorAt, failedAt)
                        .SetProperty(s => s.ConsecutiveFailures, s => s.ConsecutiveFailures + 1),
                    CancellationToken.None);

            logger.LogError(ex, "Ingest failed for {Source}", source.Slug);
            return new SourceIngestResult(source.Slug, "failed", Detail: ex.Message);
        }
    }

    private async Task<(int Fetched, int Created, int Updated)> PersistAsync(
        Source source,
        IJobSource adapter,
        JobSourceContext context,
        CancellationToken cancellationToken)
    {
        var fetched = 0;
        var created = 0;
        var updated = 0;

        // One upstream posting can legitimately appear twice in a single run (a
        // company listed under two Tier A board tokens), and the unique index on
        // (SourceId, ExternalId) would reject the second insert.
        var pending = new Dictionary<string, Vacancy>(StringComparer.Ordinal);

        // Raw payloads are kept per posting, not per fetch. Appending a row every
        // run made the table grow with how often ingest runs rather than with how
        // many postings exist — measured at ~5.7 KB a row, a scheduled hourly
        // ingest would fill a 0.5 GB database in under four days. Replaying a
        // mapping bug needs the current shape of a posting, not every historical
        // copy of it, so the newest overwrites the previous one.
        //
        // Only the ids are loaded, never the payloads: they are about to be
        // replaced, and pulling ~6 MB of jsonb per run to immediately discard it
        // would trade one waste for another.
        var existingRaw = await db.RawPostings
            .Where(r => r.SourceId == source.Id)
            .Select(r => new { r.Id, r.ExternalId })
            .ToDictionaryAsync(r => r.ExternalId, r => r.Id, StringComparer.Ordinal, cancellationToken);

        var seenRaw = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var posting in adapter.FetchAsync(context, cancellationToken))
        {
            fetched++;
            var normalized = posting.Vacancy;
            var fetchedAt = clock.GetUtcNow();

            if (seenRaw.Add(normalized.ExternalId))
            {
                if (existingRaw.TryGetValue(normalized.ExternalId, out var rawId))
                {
                    var stub = new RawPosting
                    {
                        Id = rawId,
                        SourceId = source.Id,
                        ExternalId = normalized.ExternalId,
                        Payload = posting.Payload,
                        FetchedAt = fetchedAt,
                    };
                    db.Attach(stub);
                    db.Entry(stub).Property(r => r.Payload).IsModified = true;
                    db.Entry(stub).Property(r => r.FetchedAt).IsModified = true;
                }
                else
                {
                    db.RawPostings.Add(new RawPosting
                    {
                        SourceId = source.Id,
                        ExternalId = normalized.ExternalId,
                        Payload = posting.Payload,
                        FetchedAt = fetchedAt,
                    });
                }
            }

            if (!pending.TryGetValue(normalized.ExternalId, out var vacancy))
            {
                vacancy = await db.Vacancies.FirstOrDefaultAsync(
                    v => v.SourceId == source.Id && v.ExternalId == normalized.ExternalId,
                    cancellationToken);

                if (vacancy is null)
                {
                    vacancy = new Vacancy
                    {
                        SourceId = source.Id,
                        ExternalId = normalized.ExternalId,
                        Title = normalized.Title,
                        Url = normalized.Url,
                    };
                    db.Vacancies.Add(vacancy);
                    created++;
                }
                else
                {
                    updated++;
                }

                pending[normalized.ExternalId] = vacancy;
            }

            vacancy.Title = normalized.Title;
            vacancy.Url = normalized.Url;
            vacancy.Company = normalized.Company;
            vacancy.Location = normalized.Location;
            vacancy.WorkFormat = normalized.WorkFormat;
            vacancy.Description = normalized.Description;
            vacancy.SalaryMin = normalized.SalaryMin;
            vacancy.SalaryMax = normalized.SalaryMax;
            vacancy.Currency = normalized.Currency;
            vacancy.Seniority = normalized.Seniority;
            vacancy.PublishedAt = normalized.PublishedAt?.ToUniversalTime();
            vacancy.FetchedAt = fetchedAt;
        }

        // Stamped inside the same SaveChanges as the postings. Saved separately,
        // a failure here would leave the postings committed and LastSuccessAt
        // null — and a null one skips the min_poll_interval check entirely, so
        // the next run would hit the source again immediately. On Jobicy, whose
        // cap is one poll per hour, that is the breach the interval exists to
        // prevent.
        source.LastSuccessAt = clock.GetUtcNow();
        source.ConsecutiveFailures = 0;

        await db.SaveChangesAsync(cancellationToken);
        return (fetched, created, updated);
    }
}
