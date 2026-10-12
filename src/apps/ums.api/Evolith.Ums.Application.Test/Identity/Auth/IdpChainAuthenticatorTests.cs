namespace Evolith.Ums.Application.Test.Identity.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;

/// <summary>
/// FR-042 (ADR-UMS-097 §2.3/§2.4, slice 2b): pruebas de seguridad del fallback encadenado.
///
/// Las CLAVE son las de seguridad:
///   · <b>Fallback SOLO por indisponibilidad</b>: primario infra → intenta el siguiente; si el
///     siguiente tiene éxito → login OK.
///   · <b>NO fallback por credenciales</b> (anti credential-spraying): primario rechaza credenciales
///     → TERMINAL y el segundo IdP <b>nunca</b> se invoca.
///   · <b>Ambiguo → terminal</b>: un fallo no clasificable como infra no avanza.
///   · <b>Ciclo</b> A→B→A detectado, sin bucle; cadena agotada → 503 (AUTH_018), no 401.
///   · <b>Auditoría</b>: un evento por proveedor intentado.
/// </summary>
public class IdpChainAuthenticatorTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly ActorId Actor = ActorId.Create("test");
    private const string Credential = "callback-o-password";
    private const string ClientIp = "10.0.0.1";

    private readonly Mock<IIdpConfigurationRepository> _idpConfigRepo = new();
    private readonly Mock<IIdpAuthStrategy>            _idpStrategy   = new();
    private readonly Mock<IAuthAuditService>          _audit         = new();
    private readonly Mock<IConfigurationProvider>     _config        = new();

    public IdpChainAuthenticatorTests()
    {
        // Tope de saltos: por defecto devuelve el default pasado por el orquestador (5), salvo que un
        // test lo sobreescriba. Un método no configurado en Moq devolvería 0 → maxHops=1, así que ESTE
        // setup es necesario para que la cadena pueda avanzar en los tests de fallback.
        _config.Setup(c => c.GetValueAs<int>(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int>()))
               .Returns<string, Guid?, int>((_, _, defaultValue) => defaultValue);

        _audit.Setup(a => a.RecordAuthEventAsync(It.IsAny<AuthAuditEvent>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
    }

    private IdpChainAuthenticator CreateSut()
        => new(_idpConfigRepo.Object, _idpStrategy.Object, _audit.Object, _config.Object);

    // ── Fallback por indisponibilidad ────────────────────────────────────────────

    [Fact]
    public async Task Fallback_PrimaryInfraUnavailable_AdvancesAndSecondSucceeds()
    {
        var tenant = BuildTenant(IdpStrategy.AzureAd, IdpStrategy.Keycloak);
        // A (AzureAd, prio 1) → B (Keycloak, prio 2). El selector elige A por prioridad.
        var b = BuildActiveConfig(ProviderType.Keycloak, priority: 2);
        var a = BuildActiveConfig(ProviderType.AzureAd, priority: 1);
        Link(a, b);
        SetupConfigurations(a, b);

        SetupStrategy(IdpStrategy.AzureAd, Infra("AUTH_034"));   // primario: JWKS caído
        SetupStrategy(IdpStrategy.Keycloak, Ok("user@ransa.pe")); // respaldo: éxito

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal("user@ransa.pe", result.Value.Identity.Email);
        Assert.Equal(IdpStrategy.Keycloak.Id, result.Value.Provider.Strategy.Id);
        VerifyStrategyCalled(IdpStrategy.AzureAd, Times.Once());
        VerifyStrategyCalled(IdpStrategy.Keycloak, Times.Once());
        VerifyAuditEvents(Times.Exactly(2)); // un evento por proveedor intentado
    }

    // ── NO fallback por credenciales (anti credential-spraying) — PRUEBA CLAVE ────

    [Fact]
    public async Task Credential_PrimaryRejects_Terminal_SecondNeverInvoked()
    {
        var tenant = BuildTenant(IdpStrategy.AzureAd, IdpStrategy.Keycloak);
        var b = BuildActiveConfig(ProviderType.Keycloak, priority: 2);
        var a = BuildActiveConfig(ProviderType.AzureAd, priority: 1);
        Link(a, b);
        SetupConfigurations(a, b);

        SetupStrategy(IdpStrategy.AzureAd, CredentialRejected("AUTH_021"));  // primario: el IdP RECHAZÓ la credencial
        SetupStrategy(IdpStrategy.Keycloak, Ok("attacker@ransa.pe")); // nunca debe llegar aquí

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_021", result.Error);                  // terminal, se devuelve el error del IdP
        VerifyStrategyCalled(IdpStrategy.AzureAd, Times.Once());
        VerifyStrategyCalled(IdpStrategy.Keycloak, Times.Never());  // el SEGUNDO IdP NO se intenta
        VerifyAuditEvents(Times.Exactly(1));                        // solo el intento primario
    }

    // ── Ambiguo → terminal (fail-closed) ─────────────────────────────────────────

    [Fact]
    public async Task Ambiguous_UnclassifiableFailure_Terminal_NoAdvance()
    {
        var tenant = BuildTenant(IdpStrategy.AzureAd, IdpStrategy.Keycloak);
        var b = BuildActiveConfig(ProviderType.Keycloak, priority: 2);
        var a = BuildActiveConfig(ProviderType.AzureAd, priority: 1);
        Link(a, b);
        SetupConfigurations(a, b);

        // Fallo no clasificable como infra (no está en la lista blanca) → fail-closed → terminal.
        SetupStrategy(IdpStrategy.AzureAd, Result<ExternalIdentity>.Failure("AUTH_099: fallo raro no clasificable"));
        SetupStrategy(IdpStrategy.Keycloak, Ok());

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_099", result.Error);
        VerifyStrategyCalled(IdpStrategy.Keycloak, Times.Never());
        VerifyAuditEvents(Times.Exactly(1));
    }

    // ── Ciclo A→B→A detectado; cadena agotada → 503 ──────────────────────────────

    [Fact]
    public async Task Cycle_AtoBtoA_Detected_NoInfiniteLoop_Returns503()
    {
        var tenant = BuildTenant(IdpStrategy.AzureAd, IdpStrategy.Keycloak);
        var a = BuildActiveConfig(ProviderType.AzureAd, priority: 1);
        var b = BuildActiveConfig(ProviderType.Keycloak, priority: 2);
        Link(a, b);
        Link(b, a); // ciclo
        SetupConfigurations(a, b);

        SetupStrategy(IdpStrategy.AzureAd, Infra("AUTH_034"));
        SetupStrategy(IdpStrategy.Keycloak, Infra("AUTH_012"));

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_018", result.Error);                 // cadena agotada → 503
        VerifyStrategyCalled(IdpStrategy.AzureAd, Times.Once());
        VerifyStrategyCalled(IdpStrategy.Keycloak, Times.Once());
        VerifyAuditEvents(Times.Exactly(2));                       // A y B, sin bucle infinito
    }

    // ── Cadena lineal agotada (todos infra) → 503 ────────────────────────────────

    [Fact]
    public async Task ChainExhausted_AllInfra_ReturnsServiceUnavailable()
    {
        var tenant = BuildTenant(IdpStrategy.AzureAd, IdpStrategy.Keycloak);
        var b = BuildActiveConfig(ProviderType.Keycloak, priority: 2);
        var a = BuildActiveConfig(ProviderType.AzureAd, priority: 1);
        Link(a, b);
        SetupConfigurations(a, b);

        SetupStrategy(IdpStrategy.AzureAd, Infra("AUTH_034"));
        SetupStrategy(IdpStrategy.Keycloak, Infra("AUTH_034"));

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_018", result.Error);
        VerifyAuditEvents(Times.Exactly(2));
    }

    // ── Tope de saltos configurable ──────────────────────────────────────────────

    [Fact]
    public async Task HopCap_LimitsTraversal_StopsAtCap()
    {
        var tenant = BuildTenant(IdpStrategy.AzureAd, IdpStrategy.Keycloak, IdpStrategy.Okta);
        var c = BuildActiveConfig(ProviderType.Okta, priority: 3);
        var b = BuildActiveConfig(ProviderType.Keycloak, priority: 2);
        var a = BuildActiveConfig(ProviderType.AzureAd, priority: 1);
        Link(a, b);
        Link(b, c);
        SetupConfigurations(a, b, c);

        // Tope = 1 salto: solo se intenta el primario, la cadena se corta antes del segundo.
        _config.Setup(cfg => cfg.GetValueAs<int>(IdpChainAuthenticator.MaxHopsConfigCode, It.IsAny<Guid?>(), It.IsAny<int>()))
               .Returns(1);

        SetupStrategy(IdpStrategy.AzureAd, Infra("AUTH_034"));
        SetupStrategy(IdpStrategy.Keycloak, Ok());
        SetupStrategy(IdpStrategy.Okta, Ok());

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_018", result.Error);
        VerifyStrategyCalled(IdpStrategy.AzureAd, Times.Once());
        VerifyStrategyCalled(IdpStrategy.Keycloak, Times.Never());
        VerifyAuditEvents(Times.Exactly(1));
    }

    // ── Camino feliz sin fallback ────────────────────────────────────────────────

    [Fact]
    public async Task Primary_Succeeds_NoAdvance_SingleAttempt()
    {
        var tenant = BuildTenant(IdpStrategy.AzureAd, IdpStrategy.Keycloak);
        var b = BuildActiveConfig(ProviderType.Keycloak, priority: 2);
        var a = BuildActiveConfig(ProviderType.AzureAd, priority: 1);
        Link(a, b);
        SetupConfigurations(a, b);

        SetupStrategy(IdpStrategy.AzureAd, Ok("user@ransa.pe"));

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsSuccess);
        Assert.Equal(IdpStrategy.AzureAd.Id, result.Value.Provider.Strategy.Id);
        VerifyStrategyCalled(IdpStrategy.Keycloak, Times.Never());
        VerifyAuditEvents(Times.Exactly(1));
    }

    // ── Legado: sin IdpConfiguration gobernante → intento único, sin 503 ──────────

    [Fact]
    public async Task Legacy_NoGoverningConfig_SingleAttempt_Succeeds()
    {
        var tenant = BuildTenant(IdpStrategy.AzureAd);
        SetupConfigurations(); // sin configuraciones → path legado

        SetupStrategy(IdpStrategy.AzureAd, Ok("user@ransa.pe"));

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsSuccess);
        Assert.Equal(IdpStrategy.AzureAd.Id, result.Value.Provider.Strategy.Id);
        VerifyAuditEvents(Times.Exactly(1));
    }

    [Fact]
    public async Task Legacy_NoGoverningConfig_InfraFailure_ReturnsRawError_NotChainExhausted()
    {
        // Sin cadena no hay 503: el intento único devuelve el error crudo (comportamiento previo a 2b).
        var tenant = BuildTenant(IdpStrategy.AzureAd);
        SetupConfigurations();

        SetupStrategy(IdpStrategy.AzureAd, Infra("AUTH_034"));

        var result = await CreateSut().AuthenticateAsync(tenant, Credential, null, null, ClientIp);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_034", result.Error);
        Assert.DoesNotContain("AUTH_018", result.Error);
        VerifyAuditEvents(Times.Exactly(1));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private void SetupConfigurations(params IdpConfigurationAggregate[] configurations)
        => _idpConfigRepo.Setup(r => r.GetByTenantIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(configurations.ToList());

    private void SetupStrategy(IdpStrategy strategy, Result<ExternalIdentity> result)
        => _idpStrategy.Setup(s => s.AuthenticateAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.Is<IdentityProvider>(p => p.Strategy.Id == strategy.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void VerifyStrategyCalled(IdpStrategy strategy, Times times)
        => _idpStrategy.Verify(s => s.AuthenticateAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.Is<IdentityProvider>(p => p.Strategy.Id == strategy.Id), It.IsAny<CancellationToken>()),
            times);

    private void VerifyAuditEvents(Times times)
        => _audit.Verify(a => a.RecordAuthEventAsync(It.IsAny<AuthAuditEvent>(), It.IsAny<CancellationToken>()), times);

    private static Result<ExternalIdentity> Ok(string email = "user@ransa.pe")
        => Result<ExternalIdentity>.Success(new ExternalIdentity(email, "sub", "User", new Dictionary<string, string>()));

    private static Result<ExternalIdentity> Infra(string code)
        => Result<ExternalIdentity>.Failure($"{code}: indisponibilidad de infraestructura del IdP.");

    private static Result<ExternalIdentity> CredentialRejected(string code)
        => Result<ExternalIdentity>.Failure($"{code}: el IdP rechazó la credencial.");

    private static TenantAggregate BuildTenant(params IdpStrategy[] registeredStrategies)
    {
        var tenant = TenantAggregate.Create(
            Code.Create("TEST"),
            Name.Create("Test Tenant"),
            Evolith.Ums.Domain.Enums.OrganizationType.INTERNAL,
            Actor,
            registeredStrategies[0],
            tenantId: TenantId.Load(TenantGuid)).Value;

        foreach (var strategy in registeredStrategies)
        {
            tenant.RegisterIdentityProvider(
                Code.Create(strategy.Name.ToUpperInvariant()),
                Name.Create(strategy.Name),
                Description.Create(string.Empty),
                strategy,
                Actor);
        }

        // Se activa el primer proveedor como «primario» (el inquilino solo admite uno activo). Los demás
        // quedan registrados+inactivos: el puente 2b los usa igualmente porque la cadena la gobierna
        // IdpConfiguration.Status, no el flag de proveedor activo (ADR-UMS-097 §2.5).
        var first = tenant.IdentityProviders.First();
        tenant.ActivateIdentityProvider(first.GetId(), Actor);
        tenant.DomainEvents.MarkChangesAsCommitted();
        return tenant;
    }

    private static IdpConfigurationAggregate BuildActiveConfig(ProviderType providerType, int priority)
    {
        var config = IdpConfigurationAggregate.Create(
            TenantId.Load(TenantGuid),
            SystemSuiteId.Create(),
            providerType,
            Array.Empty<string>(),
            "{\"issuer\":\"https://idp.example\"}",
            "vault/secret/idp",
            priority,
            fallbackToId: null,
            Actor).Value;
        config.Activate(Actor);
        return config;
    }

    // Enlaza from → to por FallbackToId (setter público de Props; el enlace es inmutable vía Create,
    // así que en tests se establece directamente para poder construir cadenas y ciclos).
    private static void Link(IdpConfigurationAggregate from, IdpConfigurationAggregate to)
        => from.Props.FallbackToId = to.Props.Id.GetValue();
}
