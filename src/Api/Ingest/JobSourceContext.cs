using Api.Models;

namespace Api.Ingest;

/// <summary>
/// Everything an adapter is allowed to know about the run. Notably the
/// <see cref="Source"/> row itself: poll intervals, base URLs and compliance
/// flags are read from the row, never from a constant in the adapter.
/// </summary>
/// <param name="TargetCompanies">
/// Populated for Tier A only. An ATS board has no search endpoint, so the
/// registry is the query; Tier B adapters ignore this.
/// </param>
public sealed record JobSourceContext(Source Source, IReadOnlyList<TargetCompany> TargetCompanies);
