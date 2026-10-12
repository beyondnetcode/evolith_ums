using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;

public sealed record ResolveIdpConfigurationQuery(
    Guid TenantId,
    Guid? SystemSuiteId,
    string? EmailDomain,
    string? ProviderType) : IQuery<ResolvedIdpConfigurationDto>;
