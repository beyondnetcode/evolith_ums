using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;

public sealed record GetAllIdpConfigurationsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string Criteria = "providerType",
    string Status = "all",
    string SortBy = "resolutionPriority",
    string SortOrder = "asc",
    Guid? TenantId = null,
    Guid? SystemSuiteId = null,
    string? ProviderType = null) : IQuery<PagedResult<IdpConfigurationDto>>;
