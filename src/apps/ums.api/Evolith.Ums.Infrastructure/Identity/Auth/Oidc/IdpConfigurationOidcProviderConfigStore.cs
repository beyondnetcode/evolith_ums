using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Resuelve la <see cref="OidcProviderConfig"/> del proveedor a partir del
/// <c>IdpConfiguration</c> activo del inquilino, parseando su <c>ConfigPayload</c>
/// (ADR-UMS-094: endpoints leídos de configuración, nunca hardcodeados).
///
/// La resolución completa por prioridad/suite/dominio (FR-042) y la desencriptación
/// del secreto desde el <c>SecretRef</c> se integran en el slice 2; aquí se toma el
/// primer <c>IdpConfiguration</c> activo cuyo <c>ProviderType</c> corresponde a la
/// estrategia del proveedor, ordenado por prioridad.
/// </summary>
public sealed class IdpConfigurationOidcProviderConfigStore : IOidcProviderConfigStore
{
    private static readonly IReadOnlyDictionary<string, string> StrategyToProviderType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [IdpStrategy.Keycloak.Name]    = ProviderType.Keycloak.Name,
            [IdpStrategy.GenericOidc.Name] = ProviderType.GenericOidc.Name,
            [IdpStrategy.AzureAd.Name]     = ProviderType.AzureAd.Name,
            [IdpStrategy.Okta.Name]        = ProviderType.Okta.Name,
            [IdpStrategy.Zitadel.Name]     = ProviderType.Zitadel.Name,
            [IdpStrategy.Auth0.Name]       = ProviderType.Auth0.Name,
            [IdpStrategy.Google.Name]      = ProviderType.Google.Name,
        };

    private readonly IIdpConfigurationRepository _repository;

    public IdpConfigurationOidcProviderConfigStore(IIdpConfigurationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<OidcProviderConfig>> GetAsync(
        IdentityProvider provider,
        CancellationToken cancellationToken = default)
    {
        if (!StrategyToProviderType.TryGetValue(provider.Strategy.Name, out var providerTypeName))
        {
            return Result<OidcProviderConfig>.Failure(
                OidcAuthErrors.ConfigNotFound($"La estrategia '{provider.Strategy.Name}' no es OIDC."));
        }

        var configurations = await _repository.GetByTenantIdAsync(provider.TenantId.GetValue(), cancellationToken);

        var selected = configurations
            .Where(configuration => configuration.Status == IdpConfigStatus.Active)
            .Where(configuration => string.Equals(configuration.ProviderType.Name, providerTypeName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(configuration => configuration.ResolutionPriority)
            .ThenByDescending(configuration => configuration.Version)
            .FirstOrDefault();

        if (selected is null)
        {
            return Result<OidcProviderConfig>.Failure(
                OidcAuthErrors.ConfigNotFound($"No hay IdpConfiguration activo para '{providerTypeName}' en el inquilino."));
        }

        return OidcProviderConfigParser.Parse(selected.Props.ConfigPayload);
    }
}
