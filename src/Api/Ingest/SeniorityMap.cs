using Api.Models;

namespace Api.Ingest;

/// <summary>
/// Turns the level strings sources hand back into <see cref="Seniority"/>
/// (EM-59). Every value below was observed in live data, not guessed: the
/// tables are the ones measured across 100 Jobicy and 650 Arbeitnow postings on
/// the deployed database.
/// <para>
/// Anything unrecognised is <see cref="Seniority.Unknown"/>. That is the point
/// — a level nobody stated must not be filled in by a guess in a lookup table.
/// </para>
/// </summary>
public static class SeniorityMap
{
    /// <summary>
    /// Jobicy's <c>jobLevel</c>, present on 100 of 100 postings. Five values
    /// were observed: Senior 39, Director 32, Any 22, "Entry-Level, Junior" 4,
    /// Midweight 3.
    /// <para>
    /// <c>Any</c> deliberately maps to Unknown. It means the employer did not
    /// restrict the level, which is not the same as saying the role suits a
    /// junior.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, Seniority> Jobicy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Entry-Level, Junior"] = Seniority.Junior,
        ["Entry-Level"] = Seniority.Junior,
        ["Junior"] = Seniority.Junior,
        ["Midweight"] = Seniority.Mid,
        ["Senior"] = Seniority.Senior,
        ["Director"] = Seniority.Lead,
        ["Any"] = Seniority.Unknown,
    };

    /// <summary>
    /// Arbeitnow's <c>job_types</c>, a mixed list per posting. It carries
    /// contract type and level in the same array and in two languages, so the
    /// entries that mean a level are picked out and the rest ignored —
    /// "Full Time" and "Permanent" say nothing about seniority.
    /// <para>
    /// Counts observed across 650 postings: Experienced 93, berufserfahren 45,
    /// Mid/mid 33, Working student 26, Entry 26, Intern 17, Internship 13.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, Seniority> Arbeitnow = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Intern"] = Seniority.Intern,
        ["Internship"] = Seniority.Intern,
        ["Praktikum"] = Seniority.Intern,
        ["Working student"] = Seniority.Intern,
        ["Werkstudent"] = Seniority.Intern,
        ["Student"] = Seniority.Intern,
        ["Entry"] = Seniority.Junior,
        ["Entry level"] = Seniority.Junior,
        ["Junior"] = Seniority.Junior,
        ["Berufseinsteiger"] = Seniority.Junior,
        ["Mid"] = Seniority.Mid,
        ["Experienced"] = Seniority.Senior,
        ["berufserfahren"] = Seniority.Senior,
        ["Senior"] = Seniority.Senior,
        ["Lead"] = Seniority.Lead,
        ["Manager"] = Seniority.Lead,
    };

    public static Seniority FromJobicy(string? jobLevel) =>
        jobLevel is not null && Jobicy.TryGetValue(jobLevel.Trim(), out var level)
            ? level
            : Seniority.Unknown;

    /// <summary>
    /// The most senior level named wins. A posting tagged both "Entry" and
    /// "Experienced" is not an entry role that happens to mention experience —
    /// reading it as junior would put it in front of exactly the wrong search.
    /// </summary>
    public static Seniority FromArbeitnow(IEnumerable<string>? jobTypes)
    {
        var best = Seniority.Unknown;

        foreach (var type in jobTypes ?? [])
        {
            if (Arbeitnow.TryGetValue(type.Trim(), out var level) && level > best)
            {
                best = level;
            }
        }

        return best;
    }
}
