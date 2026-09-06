namespace Api.Models;

/// <summary>
/// How senior a posting is, where the source says so itself (EM-59).
/// <para>
/// Only sources that return the level as a field populate this. Inferring it
/// from a description is EM-31 and belongs to Phase III, so anything not
/// stated stays <see cref="Unknown"/>.
/// </para>
/// <para>
/// <see cref="Unknown"/> is a real answer, not a missing one, and must never be
/// treated as a match: filtering for junior roles has to exclude it rather than
/// hope. Jobicy's `Any` maps here for exactly that reason — a posting open to
/// every level has not told us it is junior.
/// </para>
/// </summary>
public enum Seniority
{
    Unknown = 0,
    Intern = 1,
    Junior = 2,
    Mid = 3,
    Senior = 4,
    Lead = 5,
}
