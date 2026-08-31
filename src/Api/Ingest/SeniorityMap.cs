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
    /// Taken from the full distinct list in the deployed database, not a sample:
    /// experienced 91, berufserfahren 44, entry 31, mid 29, working student 27,
    /// intern 16, internship 16, student 16, berufseinstieg 10, and a long tail
    /// of German terms down to geschäftsleitung 3.
    /// <para>
    /// Contract types — full time 188, permanent 78, freelance 7 — say nothing
    /// about seniority and are deliberately absent, as is an empty array: 233 of
    /// 650 postings name no type at all and stay Unknown.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, Seniority> Arbeitnow = new(StringComparer.OrdinalIgnoreCase)
    {
        // Intern-grade. German working-student and dual-study terms sit here:
        // they are student contracts, not graduate roles.
        ["Intern"] = Seniority.Intern,
        ["Internship"] = Seniority.Intern,
        ["Traineeship"] = Seniority.Intern,
        ["Trainee"] = Seniority.Intern,
        ["Praktikum"] = Seniority.Intern,
        ["Working student"] = Seniority.Intern,
        ["Werkstudent"] = Seniority.Intern,
        ["Student"] = Seniority.Intern,
        ["Student school"] = Seniority.Intern,
        ["Hilfstätigkeit / Student"] = Seniority.Intern,
        ["Dual studies"] = Seniority.Intern,
        ["Combined-study"] = Seniority.Intern,

        ["Entry"] = Seniority.Junior,
        ["Entry level"] = Seniority.Junior,
        ["Junior"] = Seniority.Junior,
        ["Berufseinstieg"] = Seniority.Junior,
        ["Berufseinsteiger"] = Seniority.Junior,

        ["Mid"] = Seniority.Mid,
        ["Associate"] = Seniority.Mid,

        ["Senior"] = Seniority.Senior,
        ["Experienced"] = Seniority.Senior,
        ["Professional / experienced"] = Seniority.Senior,
        ["berufserfahren"] = Seniority.Senior,
        ["Mid-senior"] = Seniority.Senior,

        ["Lead"] = Seniority.Lead,
        ["Manager"] = Seniority.Lead,
        ["Executive"] = Seniority.Lead,
        ["Teamleitung"] = Seniority.Lead,
        ["Geschäftsleitung"] = Seniority.Lead,
    };

    /// <summary>
    /// Split, not matched whole. The observed value "Entry-Level, Junior" proves
    /// the field is a comma-joined list, and matching the whole string meant any
    /// combination nobody had enumerated — "Senior, Director", or either pair in
    /// the other order — fell through to Unknown and dropped the posting out of
    /// every level filter.
    /// </summary>
    public static Seniority FromJobicy(string? jobLevel) =>
        Highest(jobLevel?.Split(',') ?? [], Jobicy);

    public static Seniority FromArbeitnow(IEnumerable<string>? jobTypes) =>
        Highest(jobTypes ?? [], Arbeitnow);

    /// <summary>
    /// The most senior level named wins. A posting tagged both "Entry" and
    /// "Experienced" is not an entry role that happens to mention experience —
    /// reading it as junior would put it in front of exactly the wrong search.
    /// Unrecognised entries contribute nothing rather than resetting the answer.
    /// </summary>
    private static Seniority Highest(IEnumerable<string> values, Dictionary<string, Seniority> map)
    {
        var best = Seniority.Unknown;

        foreach (var value in values)
        {
            if (map.TryGetValue(value.Trim(), out var level) && level > best)
            {
                best = level;
            }
        }

        return best;
    }
}
