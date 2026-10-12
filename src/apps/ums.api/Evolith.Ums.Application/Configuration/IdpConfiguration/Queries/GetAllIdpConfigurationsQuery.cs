using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;

public sealed record GetAllIdpConfigurationsQuery(
    int Page = QueryDefaults.Page,
    int PageSize = QueryDefaults.PageSize,
    string? Search = null,
    string? Criteria = null,
    string? Status = null,
    string? SortBy = null,
    string? SortOrder = null,
    Guid? TenantId = null,
    Guid? SystemSuiteId = null,
    string? ProviderType = null) : IQuery<PagedResult<IdpConfigurationDto>>;
