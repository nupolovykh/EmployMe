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
        // Tier C and D are refused by tier, before Enabled is read, because a
        // boolean is the wrong thing to rest this on: Tier D is disqualified and
        // Tier C needs an approval this schema has nowhere to record. Both were
        // reachable through a single UPDATE ... SET Enabled = true until the tier
        // itself carried the refusal.
        if (source.Tier is SourceTier.C or SourceTier.D)
        {
            return new SourceIngestResult(
                source.Slug, "skipped", Detail: $"tier {source.Tier} is never ingested");
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

        // Nothing used to stop two runs overlapping. LastSuccessAt is written only
        // at the very end of PersistAsync, with the whole fetch sitting between the
        // interval check above and that write, so two concurrent calls both read the
        // pre-run value, both concluded the interval had elapsed, and both hit the
        // source inside its own cap. On Jobicy, whose cap is one poll per hour, that
        // is the breach the interval exists to prevent — reachable by a double-click
        // or a retry after a platform timeout, with force=true never involved.
        //
        // A compare-and-set on LastSuccessAt was tried first and does not hold: the
        // second run reads the value the first just wrote, so its own CAS matches
        // and it proceeds. Measured, not reasoned about — two concurrent forced
        // ingests both fetched 100 postings. Exclusion has to last for the duration
        // of the run, which is what a session-scoped advisory lock gives.
        if (!await TryAcquireLockAsync(source.Id, cancellationToken))
        {
            return new SourceIngestResult(
                source.Slug, "skipped", Detail: "another ingest run holds this source");
        }

        try
        {
            // Watch rows are still fetched: they are companies worth working for
            // that simply have no fitting role open right now, and noticing when
            // that changes is the point of the registry. Only Rejected is excluded.
            var targets = source.Tier == SourceTier.A
                ? await db.TargetCompanies
                    .Where(t => t.SourceId == source.Id && t.Status != TargetCompanyStatus.Rejected)
                    .ToListAsync(cancellationToken)
                : [];

            var context = new JobSourceContext(source, targets);

            var (fetched, created, updated) = await PersistAsync(source, adapter, context, cancellationToken);

            logger.LogInformation(
                "Ingested {Source}: {Fetched} fetched, {Created} created, {Updated} updated",
                source.Slug, fetched, created, updated);

            return new SourceIngestResult(source.Slug, "ok", fetched, created, updated);
        }
        // Only the caller giving up re-throws. `ex is not OperationCanceledException`
        // looked equivalent and is not: HttpClient's timeout surfaces as
        // TaskCanceledException, which derives from it, so a source that simply
        // hung was the one failure this handler let past — aborting the whole run
        // and skipping every source ordered after it, with no error recorded.
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            // The failed SaveChanges left this source's postings in the change
            // tracker, and they must not be retried inside the next source's save.
            // Only those are detached: Clear() would also detach every other Source
            // loaded by RunAsync, and a later success would then quietly fail to
            // persist its LastSuccessAt — leaving the poll interval unenforced for
            // a source that was working fine.
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is not Source).ToList())
            {
                entry.State = EntityState.Detached;
            }

            // This source's own row may carry a half-written success from the
            // moment before the throw, so it is reloaded rather than left dirty.
            await db.Entry(source).ReloadAsync(CancellationToken.None);

            var failedAt = clock.GetUtcNow();
            await db.Sources
                .Where(s => s.Id == source.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(s => s.LastErrorAt, failedAt)
                        .SetProperty(s => s.ConsecutiveFailures, s => s.ConsecutiveFailures + 1),
                    CancellationToken.None);

            logger.LogError(ex, "Ingest failed for {Source}", source.Slug);

            // The exception is logged in full one line above. It is not repeated
            // here: Npgsql and HttpClient messages carry host names, ports,
            // usernames and SQL fragments, and this report is returned over HTTP.
            return new SourceIngestResult(source.Slug, "failed", Detail: "see server logs");
        }
        finally
        {
            await ReleaseLockAsync(source.Id);
        }
    }

    /// <summary>
    /// Keeps ingest's advisory locks in their own key space, so locking source id
    /// 3 cannot collide with an unrelated caller locking the bare integer 3.
    /// </summary>
    private const int LockNamespace = 0x656D_6749;

    /// <summary>
    /// The connection is pinned open deliberately: a PostgreSQL advisory lock
    /// belongs to the session holding it, and EF would otherwise hand the
    /// connection back to the pool between commands and drop the lock with it.
    /// </summary>
    private async Task<bool> TryAcquireLockAsync(int sourceId, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);

        var acquired = await db.Database
            .SqlQuery<bool>($"SELECT pg_try_advisory_lock({LockNamespace}, {sourceId}) AS \"Value\"")
            .SingleAsync(cancellationToken);

        if (!acquired)
        {
            await db.Database.CloseConnectionAsync();
        }

        return acquired;
    }

    /// <summary>
    /// Released explicitly rather than left to the pool. Npgsql's DISCARD ALL on
    /// return would drop it too, but only once the connection is actually
    /// recycled, which is not a guarantee worth resting a rate limit on.
    /// </summary>
    private async Task ReleaseLockAsync(int sourceId)
    {
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_unlock({LockNamespace}, {sourceId})", CancellationToken.None);

        await db.Database.CloseConnectionAsync();
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

        // Same one-query lookup for vacancies. This path used to issue a query per
        // posting — ~650 sequential round trips for one Arbeitnow run, each paying
        // the distance to the database. Only the ids are loaded: every field is
        // overwritten from the upstream payload below, so the stored values are
        // never read.
        var existingVacancies = await db.Vacancies
            .Where(v => v.SourceId == source.Id)
            .Select(v => new { v.Id, v.ExternalId })
            .ToDictionaryAsync(v => v.ExternalId, v => v.Id, StringComparer.Ordinal, cancellationToken);

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
                if (existingVacancies.TryGetValue(normalized.ExternalId, out var vacancyId))
                {
                    vacancy = new Vacancy
                    {
                        Id = vacancyId,
                        SourceId = source.Id,
                        ExternalId = normalized.ExternalId,
                        Title = normalized.Title,
                        Url = normalized.Url,
                    };
                    db.Attach(vacancy);

                    // Marked modified wholesale, not field by field. The stub
                    // carries the upstream Title and Url already, and Attach takes
                    // whatever it is handed as the original values — so a changed
                    // title would compare equal to itself and never be written.
                    db.Entry(vacancy).State = EntityState.Modified;
                    updated++;
                }
                else
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

        // The failure counter is only reset by a run that actually returned
        // something. A source whose board tokens have all rotated, or whose
        // response no longer has the shape the adapter looks for, completes
        // without throwing and yields nothing — the per-board handlers log a
        // warning and carry on by design. Resetting on that would silence the
        // only broken-source signal this project has in exactly the case it
        // exists for. LastSuccessAt is still stamped: we did reach the source,
        // and not stamping it would skip the poll interval on the next run.
        if (fetched > 0)
        {
            source.ConsecutiveFailures = 0;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (fetched, created, updated);
    }
}
