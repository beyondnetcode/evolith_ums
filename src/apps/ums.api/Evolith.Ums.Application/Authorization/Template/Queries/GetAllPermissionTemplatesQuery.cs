using Evolith.Ums.Application.Authorization.Template.DTOs;

namespace Evolith.Ums.Application.Authorization.Template.Queries;

public sealed record GetAllPermissionTemplatesQuery(
    int Page = QueryDefaults.Page,
    int PageSize = QueryDefaults.PageSize,
    string? Search = null,
    string? Criteria = null,
    string? Status = null,
    string? SortBy = null,
    string? SortOrder = null,
    Guid? TenantId = null,
    Guid? SystemSuiteId = null,
    Guid? RoleId = null) : IQuery<PagedResult<PermissionTemplateDto>>;
