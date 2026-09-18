using Api.Ingest;
using Api.Models;

namespace Api.Tests.Ingest;

public class SeniorityMapTests
{
    [Theory]
    [InlineData("Senior", Seniority.Senior)]
    [InlineData("Entry-Level, Junior", Seniority.Junior)]
    [InlineData("Midweight", Seniority.Mid)]
    [InlineData("Director", Seniority.Lead)]
    [InlineData("Any", Seniority.Unknown)]
    [InlineData(null, Seniority.Unknown)]
    [InlineData("Wizard", Seniority.Unknown)]
    public void Jobicy_levels(string? level, Seniority expected) =>
        Assert.Equal(expected, SeniorityMap.FromJobicy(level));

    [Fact]
    public void Arbeitnow_takes_the_most_senior_of_several_levels() =>
        Assert.Equal(Seniority.Senior, SeniorityMap.FromArbeitnow(["Full Time", "Entry", "Experienced"]));

    [Fact]
    public void Arbeitnow_contract_types_alone_stay_unknown() =>
        Assert.Equal(Seniority.Unknown, SeniorityMap.FromArbeitnow(["Full Time", "Permanent"]));
}
