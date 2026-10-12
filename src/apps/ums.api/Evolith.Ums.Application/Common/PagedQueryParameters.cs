namespace Evolith.Ums.Application.Common;

/// <summary>
/// REC-12: Common pagination and filtering parameters for GetAll repository methods.
/// Passed from query handlers to repositories so SQL implementations can push
/// filtering/sorting/paging to the database (Skip/Take) rather than loading all rows.
/// </summary>
public sealed record PagedQueryParameters(
    int Page,
    int PageSize,
    string? Search = null,
    string? SortBy = null,
    string? SortOrder = null,
    string? Status = null);
