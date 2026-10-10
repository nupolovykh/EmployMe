using Api.Models;

namespace Api.Tests.Support;

/// <summary>
/// In-memory Source and TargetCompany rows for adapter tests that never touch
/// a database. Values mirror the seeded rows in src/Api/Data/Migrations.
/// </summary>
public static class TestSources
{
    public static Source Source(string slug, SourceTier tier = SourceTier.B, string? baseUrl = null) => new()
    {
        Id = slug.GetHashCode() & 0x7fff,
        Slug = slug,
        DisplayName = slug,
        Tier = tier,
        BaseUrl = baseUrl,
        AdapterType = slug,
        MinPollInterval = TimeSpan.FromHours(1),
        PublicDeployEnabled = true,
        Enabled = true,
    };

    public static TargetCompany Company(string name, string token) => new()
    {
        CompanyName = name,
        BoardToken = token,
        WhyTarget = "test",
        Status = TargetCompanyStatus.Active,
    };
}
