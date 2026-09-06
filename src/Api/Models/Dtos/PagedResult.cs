namespace Api.Models.Dtos;

/// <summary>
/// A page of results plus the total the query matched. The list endpoint is
/// paged from its first commit, and a bare JSON array cannot tell a client
/// whether it is holding 20 of 20 or 20 of 1,021 — so it cannot decide whether
/// to offer a next page.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
