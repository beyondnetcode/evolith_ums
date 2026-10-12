using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.AppConfiguration.Queries;

public sealed record GetAllAppConfigurationsQuery(
    int Page = QueryDefaults.Page,
    int PageSize = QueryDefaults.PageSize,
    string? Search = null,
    string? Criteria = null,
    string? Status = null,
    string? SortBy = null,
    string? SortOrder = null,
    string? Scope = null,
    Guid? TenantId = null,
    Guid? SystemSuiteId = null,
    Guid? ModuleId = null) : IQuery<PagedResult<AppConfigurationDto>>;
