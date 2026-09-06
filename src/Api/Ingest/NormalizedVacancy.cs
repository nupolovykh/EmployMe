namespace Api.Ingest;

/// <summary>
/// The shape every adapter maps its upstream payload onto. Deliberately the
/// lowest common denominator across the four MVP sources — A-009 tracks whether
/// this survives contact with Phase III's fit-score.
/// </summary>
public sealed record NormalizedVacancy
{
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? Company { get; init; }
    public string? Location { get; init; }
    public string? WorkFormat { get; init; }
    public string? Description { get; init; }
    public int? SalaryMin { get; init; }
    public int? SalaryMax { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>
    /// Only where the source states it. Unknown is the default and is never
    /// treated as a match — see <see cref="Api.Models.Seniority"/>.
    /// </summary>
    public Api.Models.Seniority Seniority { get; init; } = Api.Models.Seniority.Unknown;
}
