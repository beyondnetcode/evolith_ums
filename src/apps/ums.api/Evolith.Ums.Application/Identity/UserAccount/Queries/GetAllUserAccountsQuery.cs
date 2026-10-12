using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Queries;

public sealed record GetAllUserAccountsQuery(
    int Page = QueryDefaults.Page,
    int PageSize = QueryDefaults.PageSize,
    string? Search = null,
    string? Criteria = null,
    string? Status = null,
    string? SortBy = null,
    string? SortOrder = null,
    Guid? TenantId = null) : IQuery<PagedResult<UserAccountDto>>;
