using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Services;

public interface IIdpConfigurationResolver
{
    Task<Result<ResolvedIdpConfigurationDto>> ResolveAsync(
        Guid tenantId,
        Guid? systemSuiteId,
        string? emailDomain,
        string? providerType,
        CancellationToken cancellationToken = default);
}
