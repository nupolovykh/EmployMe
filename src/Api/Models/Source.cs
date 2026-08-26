namespace Api.Models;

public enum SourceTier
{
    A, // employer ATS board, published by the hiring company itself
    B, // public remote-job API, free/keyless, display conditional on attribution
    C, // requires registration, an API key, or partner approval — out of scope for the MVP
    D, // restricted or disqualified — PublicDeployEnabled must be false for every row at this tier
}

public enum SourceAuthKind
{
    None,
    ApiKey,
    OAuth,
}

public class Source
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string DisplayName { get; set; }
    public SourceTier Tier { get; set; }

    public string? BaseUrl { get; set; }
    public required string AdapterType { get; set; }
    public SourceAuthKind AuthKind { get; set; }

    // Enforced by the scheduler, not by convention — see docs/SOURCES.md.
    public TimeSpan MinPollInterval { get; set; }

    public bool AttributionRequired { get; set; }
    public string? AttributionHtml { get; set; }
    public bool CanonicalUrlRequired { get; set; }

    public string? TermsUrl { get; set; }
    public DateTimeOffset? TermsReviewedAt { get; set; }

    // Tier D rows must always have this false; see docs/SOURCES.md's disqualified-sources section.
    public bool PublicDeployEnabled { get; set; }
    public bool Enabled { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastErrorAt { get; set; }
    public int ConsecutiveFailures { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Vacancy> Vacancies { get; set; } = [];
    public ICollection<RawPosting> RawPostings { get; set; } = [];
}
