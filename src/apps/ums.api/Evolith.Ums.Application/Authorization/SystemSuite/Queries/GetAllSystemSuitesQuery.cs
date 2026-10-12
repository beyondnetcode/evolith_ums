using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Queries;

public sealed record GetAllSystemSuitesQuery(
    int Page = QueryDefaults.Page,
    int PageSize = QueryDefaults.PageSize,
    string? Search = null,
    string? Criteria = null,
    string? Status = null,
    string? SortBy = null,
    string? SortOrder = null,
    Guid? TenantId = null) : IQuery<PagedResult<SystemSuiteDto>>;
