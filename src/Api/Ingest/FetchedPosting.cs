namespace Api.Ingest;

/// <summary>
/// One upstream posting: the verbatim payload it arrived as, plus the mapping of
/// it. Both are kept — <see cref="Payload"/> lands in <c>raw_postings</c> so a
/// mapping bug is replayable without re-hitting a rate-limited source.
/// </summary>
public sealed record FetchedPosting(NormalizedVacancy Vacancy, string Payload);
