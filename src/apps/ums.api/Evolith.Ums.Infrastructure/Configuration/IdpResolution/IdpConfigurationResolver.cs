using BeyondNetCode.Shell.Factory.Interfaces;
using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Services;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Enums;

namespace Evolith.Ums.Infrastructure.Configuration.IdpResolution;

internal sealed class IdpConfigurationResolver : IIdpConfigurationResolver
{
    private readonly IIdpConfigurationRepository _repository;
    private readonly IFactory _factory;

    public IdpConfigurationResolver(IIdpConfigurationRepository repository, IFactory factory)
    {
        _repository = repository;
        _factory = factory;
    }

    public async Task<Result<ResolvedIdpConfigurationDto>> ResolveAsync(
        Guid tenantId,
        Guid? systemSuiteId,
        string? emailDomain,
        string? providerType,
        CancellationToken cancellationToken = default)
    {
        var configurations = await _repository.GetByTenantIdAsync(tenantId, cancellationToken);

        // La regla de selección de FR-042 es única y vive en el dominio; el motor de consulta OIDC
        // y la resolución del login la comparten para aplicar exactamente el mismo criterio.
        var selection = IdpConfigurationSelector.Select(configurations, systemSuiteId, emailDomain, providerType);

        if (selection is null)
        {
            return Result<ResolvedIdpConfigurationDto>.Failure("Active IdP configuration not found.");
        }

        var selected = selection.Value.Configuration;

        var strategy = _factory.Create<IdpResolutionStrategyCriteria, IIdpResolutionStrategy>(
            new IdpResolutionStrategyCriteria(selected.ProviderType.Name))
            .SingleOrDefault();

        if (strategy is null)
        {
            return Result<ResolvedIdpConfigurationDto>.Failure($"No IdP resolution strategy is registered for provider type '{selected.ProviderType.Name}'.");
        }

        return Result<ResolvedIdpConfigurationDto>.Success(strategy.Resolve(new IdpResolutionContext(selected, selection.Value.DomainMatched)));
    }
}
