using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.AppConfiguration.Queries;

public sealed record GetAllAppConfigurationsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string Criteria = "code",
    string Status = "all",
    string SortBy = "code",
    string SortOrder = "asc",
    string? Scope = null,
    Guid? TenantId = null,
    Guid? SystemSuiteId = null,
    Guid? ModuleId = null) : IQuery<PagedResult<AppConfigurationDto>>;
