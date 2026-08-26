namespace Api.Models;

public enum HiringGeo
{
    GlobalRemote,
    EuRemote,
    RelocationSponsor,
    OnsiteOnly,
}

public enum TargetCompanyStatus
{
    Active,
    Watch,
    Rejected,
}

/// <summary>
/// Tier A has no search — it fetches per company. One row per company registered against a
/// Tier A Source (Greenhouse/Lever/...); the ATS itself comes from the Source FK, not duplicated
/// here. See docs/SOURCES.md's "Target-company registry" section (EM-50).
/// </summary>
public class TargetCompany
{
    public int Id { get; set; }

    public int SourceId { get; set; }
    public Source? Source { get; set; }

    public required string CompanyName { get; set; }
    public required string BoardToken { get; set; }
    public required string WhyTarget { get; set; }

    public HiringGeo HiringGeo { get; set; }
    public TargetCompanyStatus Status { get; set; }

    public DateTimeOffset VerifiedAt { get; set; }
    public int JobsSeen { get; set; }
}
