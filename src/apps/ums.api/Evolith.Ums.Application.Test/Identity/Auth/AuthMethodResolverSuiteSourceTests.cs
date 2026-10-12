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
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;

/// <summary>
/// G-110 (residual del slice 2a de FR-042, ADR-UMS-097 §2.2): la procedencia de la suite
/// pre-autenticación. El <c>AuthMethodResolverService</c> ya aceptaba <c>systemSuiteId</c>, pero por
/// la ruta REST real llegaba <c>null</c> porque ni el <c>AuthAccessScope</c> ni el <c>Tenant</c> lo
/// alimentaban. Estas pruebas ejercen la <b>fuente nueva</b> (suite por defecto del inquilino): sin
/// pasar suite explícita (como hace la ruta REST), el filtro por suite del <c>IdpConfigurationSelector</c>
/// se ejerce de verdad — una <c>IdpConfiguration</c> de otra suite NO se elige; la de la suite correcta sí.
/// Y la no-regresión: sin suite por defecto ⇒ filtro omitido (comportamiento previo); portal ⇒ local.
/// </summary>
public class AuthMethodResolverSuiteSourceTests
{
    private readonly Mock<IConfigurationProvider>      _config        = new();
    private readonly Mock<ITenantRepository>           _tenantRepo    = new();
    private readonly Mock<IIdpConfigurationRepository> _idpConfigRepo = new();
    private readonly Guid                              _tenantId      = Guid.NewGuid();

    private AuthMethodResolverService CreateSut()
        => new(_config.Object, _tenantRepo.Object, _idpConfigRepo.Object);

    private void SetupAuthUseExternalIdp(bool value)
        => _config.Setup(c => c.GetWithPrecedence(AppConfigurationCodes.AuthUseExternalIdp, _tenantId))
                  .Returns(BuildConfiguration(_tenantId, value));

    private void SetupIdpConfigurations(params IdpConfigurationAggregate[] configurations)
        => _idpConfigRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(configurations.ToList());

    private void SetupTenant(TenantAggregate tenant)
        => _tenantRepo.Setup(r => r.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(tenant);

    // ── Fuente de suite: el default del inquilino alimenta el filtro por suite ──────────

    [Fact]
    public async Task ResolveAsync_NoScopeSuite_UsesTenantDefaultSuite_SelectsMatchingSuiteConfig()
    {
        // Ruta REST realista: NO se pasa suite (systemSuiteId == null). El inquilino tiene suite por
        // defecto = suiteB y proveedor Keycloak activo. Dos configs: AzureAd@suiteA con MEJOR prioridad
        // (1) y Keycloak@suiteB con PEOR prioridad (10). Sin filtro de suite ganaría AzureAd@suiteA por
        // prioridad; PERO al poblarse la suite desde el default del inquilino (suiteB), el filtro EXCLUYE
        // la config de la otra suite y elige Keycloak@suiteB pese a su peor prioridad. Prueba que el
        // SystemSuiteId llega no-null al selector desde una fuente real y que el filtro por suite se ejerce.
        var suiteA = Guid.NewGuid();
        var suiteB = Guid.NewGuid();
        SetupAuthUseExternalIdp(true);
        SetupTenant(BuildTenantWithActiveIdpAndDefaultSuite(IdpStrategy.Keycloak, defaultSuiteId: suiteB));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.AzureAd, priority: 1, suiteId: suiteA),
            BuildActiveConfig(ProviderType.Keycloak, priority: 10, suiteId: suiteB));

        var result = await CreateSut().ResolveAsync(
            _tenantId, AuthAccessScope.ExternalApi, systemSuiteId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.IDP, result.Value.Type);
        Assert.NotNull(result.Value.Provider);
        Assert.Equal(IdpStrategy.Keycloak, result.Value.Provider!.Strategy);
    }

    [Fact]
    public async Task ResolveAsync_NoScopeSuite_TenantDefaultSelectsOtherSuiteConfig_NotForcedToActiveProvider()
    {
        // Mismo par de configs, pero ahora el default del inquilino = suiteA. El filtro por suite elige
        // AzureAd@suiteA; como el proveedor activo es Keycloak (no AzureAd), el puente no casa y NO se
        // sustituye por otro proveedor (autenticar contra un IdP no elegido sería incorrecto) → Local.
        // Contrasta con la prueba anterior: cambiar SOLO la suite por defecto cambia la config elegida,
        // lo que demuestra que la suite del inquilino gobierna la selección.
        var suiteA = Guid.NewGuid();
        var suiteB = Guid.NewGuid();
        SetupAuthUseExternalIdp(true);
        SetupTenant(BuildTenantWithActiveIdpAndDefaultSuite(IdpStrategy.Keycloak, defaultSuiteId: suiteA));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.AzureAd, priority: 1, suiteId: suiteA),
            BuildActiveConfig(ProviderType.Keycloak, priority: 10, suiteId: suiteB));

        var result = await CreateSut().ResolveAsync(
            _tenantId, AuthAccessScope.ExternalApi, systemSuiteId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.Local, result.Value.Type);
    }

    [Fact]
    public async Task ResolveAsync_ScopeSuiteProvided_TakesPrecedenceOverTenantDefault()
    {
        // Si el AccessScope SÍ fija suite (systemSuiteId != null), esa suite manda sobre el default del
        // inquilino (ADR-UMS-097 §2.2: "proviene del AccessScope; si el scope no la fija, el default"). El
        // inquilino tiene default = suiteA, pero se pasa suiteB explícita → gana Keycloak@suiteB.
        var suiteA = Guid.NewGuid();
        var suiteB = Guid.NewGuid();
        SetupAuthUseExternalIdp(true);
        SetupTenant(BuildTenantWithActiveIdpAndDefaultSuite(IdpStrategy.Keycloak, defaultSuiteId: suiteA));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.AzureAd, priority: 1, suiteId: suiteA),
            BuildActiveConfig(ProviderType.Keycloak, priority: 10, suiteId: suiteB));

        var result = await CreateSut().ResolveAsync(
            _tenantId, AuthAccessScope.ExternalApi, systemSuiteId: suiteB);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.IDP, result.Value.Type);
        Assert.Equal(IdpStrategy.Keycloak, result.Value.Provider!.Strategy);
    }

    // ── No-regresión ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_NoScopeSuite_NoTenantDefault_OmitsSuiteFilter_PreviousBehavior()
    {
        // Inquilino SIN suite por defecto (suite única) y sin suite en el scope → efectivo == null →
        // el selector OMITE el filtro por suite y gana por prioridad global (AzureAd@suiteA, prioridad 1),
        // que casa con el proveedor activo AzureAd. Comportamiento previo intacto (retrocompatible).
        var suiteA = Guid.NewGuid();
        var suiteB = Guid.NewGuid();
        SetupAuthUseExternalIdp(true);
        SetupTenant(BuildTenantWithActiveIdpAndDefaultSuite(IdpStrategy.AzureAd, defaultSuiteId: null));
        SetupIdpConfigurations(
            BuildActiveConfig(ProviderType.AzureAd, priority: 1, suiteId: suiteA),
            BuildActiveConfig(ProviderType.Keycloak, priority: 10, suiteId: suiteB));

        var result = await CreateSut().ResolveAsync(
            _tenantId, AuthAccessScope.ExternalApi, systemSuiteId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.IDP, result.Value.Type);
        Assert.Equal(IdpStrategy.AzureAd, result.Value.Provider!.Strategy);
    }

    [Fact]
    public async Task ResolveAsync_PortalManagement_WithTenantDefaultSuite_StillForcesLocal()
    {
        // ADR-UMS-072: el portal de gestión interna SIEMPRE resuelve a local, incluso si el inquilino
        // tiene suite por defecto. La fuente de suite no debe filtrarse al portal ni consultar el repo.
        SetupAuthUseExternalIdp(true);
        SetupTenant(BuildTenantWithActiveIdpAndDefaultSuite(IdpStrategy.Keycloak, defaultSuiteId: Guid.NewGuid()));

        var result = await CreateSut().ResolveAsync(_tenantId, AuthAccessScope.PortalManagement, systemSuiteId: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthMethodType.Local, result.Value.Type);
        Assert.Null(result.Value.Provider);
        _tenantRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────

    private static TenantAggregate BuildTenantWithActiveIdpAndDefaultSuite(IdpStrategy strategy, Guid? defaultSuiteId)
    {
        var actor  = ActorId.Create("test");
        var tenant = TenantAggregate.Create(
            Code.Create("TEST"),
            Name.Create("Test Tenant"),
            Evolith.Ums.Domain.Enums.OrganizationType.INTERNAL,
            actor,
            strategy).Value;

        if (defaultSuiteId.HasValue)
        {
            tenant.SetDefaultSystemSuite(SystemSuiteId.Load(defaultSuiteId.Value), actor);
        }

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

    private IdpConfigurationAggregate BuildActiveConfig(
        ProviderType providerType,
        int priority,
        Guid suiteId,
        string[]? domainHints = null)
    {
        var actor = ActorId.Create("test");
        var config = IdpConfigurationAggregate.Create(
            TenantId.Load(_tenantId),
            SystemSuiteId.Load(suiteId),
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
