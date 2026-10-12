using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Queries;

public sealed record GetAllSystemSuitesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string Criteria = "name",
    string Status = "all",
    string SortBy = "name",
    string SortOrder = "asc",
    Guid? TenantId = null) : IQuery<PagedResult<SystemSuiteDto>>;
