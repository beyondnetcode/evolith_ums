using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Resolves the authentication method for a tenant.
/// Reads AUTH_USE_EXTERNAL_IDP from the in-memory IConfigurationProvider
/// (zero DB hits per request - the provider loads once and caches in memory).
///
/// - missing configuration → failure
/// - false → AuthMethod.Local()
/// - true + active IDP → AuthMethod.Idp(provider)
/// - true + no active IDP:
///   - ExternalApi     → AuthMethod.Local() (G-049)
///   - InternalPreview → AuthMethod(Type = IDP, Provider = null)
///
/// FR-042 (ADR-UMS-097, slice 2a): en modo IDP el proveedor se elige por el <b>motor de reglas</b>
/// (<see cref="IdpConfigurationSelector"/> sobre las <c>IdpConfiguration</c> del inquilino), por
/// prioridad/suite/dominio (desempate por versión), en lugar del antiguo <c>FirstOrDefault(IsActive)</c>.
/// La configuración ganadora se reconcilia con el <c>IdentityProvider</c> que consume el adaptador
/// (puente por estrategia ↔ tipo de proveedor). Si el inquilino no tiene ninguna <c>IdpConfiguration</c>
/// que gobierne la selección, se conserva el comportamiento previo (único proveedor activo).
/// No hay fallback encadenado: esto es slice 2a; un fallo de credenciales es terminal (§2.3).
/// </summary>
public sealed class AuthMethodResolverService : IAuthMethodResolver
{
    private readonly IConfigurationProvider      _config;
    private readonly ITenantRepository           _tenantRepo;
    private readonly IIdpConfigurationRepository _idpConfigRepo;

    public AuthMethodResolverService(
        IConfigurationProvider config,
        ITenantRepository tenantRepo,
        IIdpConfigurationRepository idpConfigRepo)
    {
        _config        = config;
        _tenantRepo    = tenantRepo;
        _idpConfigRepo = idpConfigRepo;
    }

    public async Task<Result<AuthMethod>> ResolveAsync(
        Guid tenantId,
        AuthAccessScope scope,
        Guid? systemSuiteId = null,
        string? emailDomain = null,
        CancellationToken cancellationToken = default)
    {
        if (scope == AuthAccessScope.PortalManagement)
        {
            return Result<AuthMethod>.Success(AuthMethod.Local());
        }

        var configured = _config.GetWithPrecedence(AppConfigurationCodes.AuthUseExternalIdp, tenantId);
        if (configured is null)
        {
            return Result<AuthMethod>.Failure(
                $"REQUIRED_PARAMETER_NOT_CONFIGURED: Parameter '{AppConfigurationCodes.AuthUseExternalIdp}' is not configured for tenant '{tenantId}'.");
        }

        if (!bool.TryParse(configured.Props.Value.GetValue(), out var useExternalIdp))
        {
            return Result<AuthMethod>.Failure(
                $"INVALID_PARAMETER_VALUE: Parameter '{AppConfigurationCodes.AuthUseExternalIdp}' must be a boolean value for tenant '{tenantId}'.");
        }

        if (!useExternalIdp)
            return Result<AuthMethod>.Success(AuthMethod.Local());

        // IDP mode — select the identity provider for this tenant by the FR-042 rules.
        var tenant = await _tenantRepo.GetByIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            return Result<AuthMethod>.Failure($"AUTH_002: Tenant {tenantId} not found.");

        // FR-042 (ADR-UMS-097 §2.2): procedencia de la suite pre-autenticación. La suite viene del
        // AccessScope que el login transporta (systemSuiteId); si el scope no la fija, se usa el
        // default del inquilino. Null ⇒ inquilino de suite única ⇒ el selector omite el filtro por suite.
        var effectiveSuiteId = systemSuiteId ?? tenant.DefaultSystemSuiteId?.GetValue();

        var provider = await SelectProviderAsync(tenant, tenantId, effectiveSuiteId, emailDomain, cancellationToken);

        if (provider is null)
        {
            if (scope == AuthAccessScope.InternalPreview)
            {
                return Result<AuthMethod>.Success(new AuthMethod(AuthMethodType.IDP));
            }

            // Fallback a Local según G-049
            return Result<AuthMethod>.Success(AuthMethod.Local());
        }

        return Result<AuthMethod>.Success(AuthMethod.Idp(provider));
    }

    /// <summary>
    /// Elige el <c>IdentityProvider</c> aplicando la regla de selección FR-042 sobre las
    /// <c>IdpConfiguration</c> del inquilino y reconciliándola con el proveedor que consume el adaptador.
    /// </summary>
    private async Task<IdentityProvider?> SelectProviderAsync(
        Evolith.Ums.Domain.Identity.Tenant.Tenant tenant,
        Guid tenantId,
        Guid? systemSuiteId,
        string? emailDomain,
        CancellationToken cancellationToken)
    {
        var configurations = await _idpConfigRepo.GetByTenantIdAsync(tenantId, cancellationToken);
        var selection = IdpConfigurationSelector.Select(configurations, systemSuiteId, emailDomain, providerType: null);

        if (selection is null)
        {
            // Ninguna IdpConfiguration gobierna la selección → se conserva el comportamiento previo
            // (único proveedor activo). No se rompe a los inquilinos aún no migrados a reglas.
            return tenant.GetActiveIdentityProvider();
        }

        // Puente reglas ↔ dominio (ADR-UMS-097 §2.5): la configuración ganadora determina el
        // IdentityProvider activo cuyo IdpStrategy corresponde al ProviderType de la regla.
        // ProviderType e IdpStrategy son enumeraciones paralelas por Id (mismo par 1..10, verificado
        // por la persistencia que las guarda por Id), lo que da un puente 1-a-1 sin Code/Name en el DTO.
        var winnerProviderTypeId = selection.Value.Configuration.ProviderType.Id;
        var bridged = tenant.IdentityProviders
            .FirstOrDefault(ip => ip.IsActive && ip.Strategy.Id == winnerProviderTypeId);

        // Si la regla eligió un proveedor que el inquilino no tiene activo, NO se sustituye por otro
        // (autenticar contra un IdP no elegido sería incorrecto): se trata como «sin proveedor usable».
        return bridged;
    }
}
