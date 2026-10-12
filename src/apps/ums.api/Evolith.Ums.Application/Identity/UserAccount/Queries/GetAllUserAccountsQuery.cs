using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Queries;

public sealed record GetAllUserAccountsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string Criteria = "email",
    string Status = "all",
    string SortBy = "email",
    string SortOrder = "asc",
    Guid? TenantId = null) : IQuery<PagedResult<UserAccountDto>>;
