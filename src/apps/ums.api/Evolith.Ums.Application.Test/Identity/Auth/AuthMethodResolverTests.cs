namespace Evolith.Ums.Application.Test.Identity.Auth;

using Moq;
using Xunit;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.AppConfiguration;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;

/// <summary>
/// Tests for AuthMethodResolverService.
/// Verifies dynamic resolution from IConfigurationProvider without hitting the DB,
/// plus FR-042 (ADR-UMS-097 slice 2a): selección del proveedor por el motor de reglas
/// (prioridad/suite/dominio) y puente reglas ↔ IdentityProvider.
/// </summary>
public class AuthMethodResolverTests
{
    private readonly Mock<IConfigurationProvider>      _config        = new();
    private readonly Mock<ITenantRepository>           _tenantRepo    = new();
    private readonly Mock<IIdpConfigurationRepository> _idpConfigRepo = new();
    private readonly Guid                              _tenantId      = Guid.NewGuid();

    public AuthMethodResolverTests()
    {
        // Por defecto no hay IdpConfiguration → el resolver conserva el comportamiento previo
        // (proveedor activo del inquilino). Los tests de reglas sobreescriben este setup.
        _idpConfigRepo.Setup(r => r.GetByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new List<IdpConfigurationAggregate>());
    }

    private AuthMethodResolverService CreateSut()
        => new(_config.Object, _tenantRepo.Object, _idpConfigRepo.Object);

    private void SetupAuthUseExternalIdp(bool value)
        => _config.Setup(c => c.GetWithPrecedence(AppConfigurationCodes.AuthUseExternalIdp, _tenantId))
                  .Returns(BuildConfiguration(_tenantId, value));

    private void SetupIdpConfigurations(params IdpConfigurationAggregate[] configurations)
        => _idpConfigRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(configurations.ToList());

    // ── Local mode ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenAuthUseExternalIdpFalse_ReturnsLocalMethod()
    {
        SetupAuthUseExternalIdp(false);

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.Local, result.Value.Type);
        Assert.Null(result.Value.Provider);
    }

    [Fact]
    public async Task ResolveAsync_WhenAuthUseExternalIdpMissing_ReturnsFailure()
    {
        _config.Setup(c => c.GetWithPrecedence(AppConfigurationCodes.AuthUseExternalIdp, _tenantId))
               .Returns((AppConfigurationAggregate?)null);

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);

        Assert.True(result.IsFailure);
        Assert.Contains("REQUIRED_PARAMETER_NOT_CONFIGURED", result.Error);
    }

    [Fact]
    public async Task ResolveAsync_LocalMode_DoesNotQueryTenantRepository()
    {
        SetupAuthUseExternalIdp(false);

        await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);

        _tenantRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── IDP mode ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenIdpConfigAndActiveIdp_ReturnsIdpMethod()
    {
        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithActiveIdp(IdpStrategy.AzureAd));

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.IDP, result.Value.Type);
        Assert.NotNull(result.Value.Provider);
    }

    [Fact]
    public async Task ResolveAsync_WhenIdpConfigButNoActiveIdp_ReturnsLocalAuth()
    {
        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithNoActiveIdp());

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.Local, result.Value.Type);
    }

    [Fact]
    public async Task ResolveAsync_InternalPreview_WhenIdpConfigButNoActiveIdp_ReturnsIdpWithoutProvider()
    {
        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithNoActiveIdp());

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.InternalPreview);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.IDP, result.Value.Type);
        Assert.Null(result.Value.Provider);
    }

    [Fact]
    public async Task ResolveAsync_UsesConfigProvider_NotHardcoded()
    {
        // The key invariant: the resolver delegates to config, not hardcoded logic.
        // Changing the config value changes the method — no code change needed.
        SetupAuthUseExternalIdp(false);
        var local = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);
        Assert.Equal(AuthMethodType.Local, local.Value.Type);

        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithActiveIdp(IdpStrategy.AzureAd));
        var idp = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);
        Assert.Equal(AuthMethodType.IDP, idp.Value.Type);
    }

    [Fact]
    public async Task ResolveAsync_PortalManagementAlwaysReturnsLocal()
    {
        SetupAuthUseExternalIdp(true);

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.PortalManagement);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.Local, result.Value.Type);
        Assert.Null(result.Value.Provider);
        _tenantRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── FR-042 (ADR-UMS-097 §2.1): selección por reglas + puente al IdentityProvider ──

    [Fact]
    public async Task ResolveAsync_RuleBasedSelection_BridgesToProviderOfWinningConfig()
    {
        // Dos configuraciones activas de distinta prioridad; el proveedor activo del inquilino
        // corresponde a la de menor orden (AzureAd, prioridad 5). El puente reglas↔proveedor casa.
        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithActiveIdp(IdpStrategy.AzureAd));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.AzureAd, priority: 5),
            BuildActiveConfig(ProviderType.Keycloak, priority: 10));

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.IDP, result.Value.Type);
        Assert.NotNull(result.Value.Provider);
        Assert.Equal(IdpStrategy.AzureAd, result.Value.Provider!.Strategy);
    }

    [Fact]
    public async Task ResolveAsync_RuleSelectsProviderNotActive_DoesNotSubstituteActiveProvider()
    {
        // La regla elige KEYCLOAK (prioridad 5), pero el único proveedor activo del inquilino es AzureAd.
        // No se sustituye por AzureAd (sería autenticar contra un IdP no elegido): se resuelve a Local.
        // Prueba de que la selección la gobiernan las reglas, no FirstOrDefault(IsActive).
        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithActiveIdp(IdpStrategy.AzureAd));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.Keycloak, priority: 5),
            BuildActiveConfig(ProviderType.AzureAd, priority: 10));

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.Local, result.Value.Type);
    }

    [Fact]
    public async Task ResolveAsync_SuiteFilter_SelectsConfigOfMatchingSuite()
    {
        // Inquilino con proveedor Keycloak activo. Dos suites: A→AzureAd, B→Keycloak (misma prioridad).
        // Filtrando por la suite B se elige la config Keycloak y el puente casa con el proveedor activo.
        var suiteA = Guid.NewGuid();
        var suiteB = Guid.NewGuid();
        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithActiveIdp(IdpStrategy.Keycloak));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.AzureAd, priority: 1, suiteId: suiteA),
            BuildActiveConfig(ProviderType.Keycloak, priority: 1, suiteId: suiteB));

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi, systemSuiteId: suiteB);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.IDP, result.Value.Type);
        Assert.Equal(IdpStrategy.Keycloak, result.Value.Provider!.Strategy);
    }

    [Fact]
    public async Task ResolveAsync_SuiteFilter_WhenMatchingConfigProviderNotActive_ReturnsLocal()
    {
        // Filtrando por la suite A la regla elige AzureAd, pero el proveedor activo es Keycloak → Local.
        var suiteA = Guid.NewGuid();
        var suiteB = Guid.NewGuid();
        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithActiveIdp(IdpStrategy.Keycloak));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.AzureAd, priority: 1, suiteId: suiteA),
            BuildActiveConfig(ProviderType.Keycloak, priority: 1, suiteId: suiteB));

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.ExternalApi, systemSuiteId: suiteA);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.Local, result.Value.Type);
    }

    [Fact]
    public async Task ResolveAsync_DomainRouting_PrefersDomainMatchedConfigOverPriority()
    {
        // Sin dominio gana Keycloak (prioridad 1). Con dominio acme.com gana AzureAd (casa por dominio,
        // aunque su prioridad 10 sea peor): el routing por dominio prima sobre la prioridad.
        SetupAuthUseExternalIdp(true);
        _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(BuildTenantWithActiveIdp(IdpStrategy.AzureAd));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.AzureAd, priority: 10, domainHints: new[] { "acme.com" }),
            BuildActiveConfig(ProviderType.Keycloak, priority: 1));

        var withDomain = await CreateSut().ResolveAsync(
            _tenantId, AuthAccessScope.ExternalApi, emailDomain: "user@acme.com");

        Assert.True(withDomain.IsSuccess);
        Assert.Equal(AuthMethodType.IDP, withDomain.Value.Type);
        Assert.Equal(IdpStrategy.AzureAd, withDomain.Value.Provider!.Strategy);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Evolith.Ums.Domain.Identity.Tenant.Tenant BuildTenantWithActiveIdp(IdpStrategy strategy)
    {
        var actor  = ActorId.Create("test");
        var tenant = Evolith.Ums.Domain.Identity.Tenant.Tenant.Create(
            Code.Create("TEST"),
            Name.Create("Test Tenant"),
            Evolith.Ums.Domain.Enums.OrganizationType.INTERNAL,
            actor,
            strategy).Value;

        tenant.RegisterIdentityProvider(
            Code.Create(strategy.Name.ToUpperInvariant()),
            Name.Create(strategy.Name),
            Description.Create(""),
            strategy,
            actor);

        var idp = tenant.IdentityProviders.First();
        tenant.ActivateIdentityProvider(idp.GetId(), actor);
        tenant.DomainEvents.MarkChangesAsCommitted();
        return tenant;
    }

    private static Evolith.Ums.Domain.Identity.Tenant.Tenant BuildTenantWithNoActiveIdp()
    {
        var actor = ActorId.Create("test");
        var tenant = Evolith.Ums.Domain.Identity.Tenant.Tenant.Create(
            Code.Create("TEST"),
            Name.Create("Test Tenant"),
            Evolith.Ums.Domain.Enums.OrganizationType.INTERNAL,
            actor,
            Evolith.Ums.Domain.Enums.IdpStrategy.AzureAd).Value;
        tenant.DomainEvents.MarkChangesAsCommitted();
        return tenant;
    }

    private IdpConfigurationAggregate BuildActiveConfig(
        ProviderType providerType,
        int priority,
        Guid? suiteId = null,
        string[]? domainHints = null)
    {
        var actor = ActorId.Create("test");
        var config = IdpConfigurationAggregate.Create(
            TenantId.Load(_tenantId),
            suiteId.HasValue ? SystemSuiteId.Load(suiteId.Value) : SystemSuiteId.Create(),
            providerType,
            domainHints ?? Array.Empty<string>(),
            "{\"issuer\":\"https://idp.example\"}",
            "vault/secret/idp",
            priority,
            null,
            actor).Value;
        config.Activate(actor);
        return config;
    }

    private static AppConfigurationAggregate BuildConfiguration(Guid tenantId, bool value)
    {
        var actor = ActorId.Create("test");
        return AppConfigurationAggregate.Create(
                TenantId.Load(tenantId),
                null,
                null,
                Code.Create(AppConfigurationCodes.AuthUseExternalIdp),
                ConfigurationValue.Create(value.ToString().ToLowerInvariant()),
                Description.Create("Use external IDP"),
                true,
                false,
                actor)
            .Value;
    }
}
