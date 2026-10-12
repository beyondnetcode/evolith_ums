namespace Evolith.Ums.Domain.Test.Configuration.IdpConfiguration;

using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Xunit;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;

/// <summary>
/// FR-042 (ADR-UMS-097 §2.1/§4): la regla pura de selección de IdP por prioridad/suite/dominio,
/// con desempate por versión. Fuente única compartida por el login y el motor de consulta OIDC.
/// </summary>
public class IdpConfigurationSelectorTests
{
    private static readonly ActorId Actor = ActorId.Create("test");
    private static readonly Guid TenantGuid = Guid.NewGuid();

    // ── Sin candidatos ──────────────────────────────────────────────────────────

    [Fact]
    public void Select_WhenNoConfigurations_ReturnsNull()
    {
        var selection = IdpConfigurationSelector.Select(
            new List<IdpConfigurationAggregate>(), systemSuiteId: null, emailDomain: null, providerType: null);

        Assert.Null(selection);
    }

    [Fact]
    public void Select_WhenOnlyDraftConfigurations_ReturnsNull()
    {
        // Draft (no activada) no es candidata.
        var draft = BuildConfig(ProviderType.Keycloak, priority: 1, activate: false);

        var selection = IdpConfigurationSelector.Select(
            new[] { draft }, systemSuiteId: null, emailDomain: null, providerType: null);

        Assert.Null(selection);
    }

    // ── Prioridad ────────────────────────────────────────────────────────────────

    [Fact]
    public void Select_ByPriority_SelectsLowestPriorityNumber()
    {
        var low = BuildConfig(ProviderType.AzureAd, priority: 5);
        var high = BuildConfig(ProviderType.Keycloak, priority: 10);

        var selection = IdpConfigurationSelector.Select(
            new[] { high, low }, systemSuiteId: null, emailDomain: null, providerType: null);

        Assert.NotNull(selection);
        Assert.Equal(ProviderType.AzureAd, selection!.Value.Configuration.ProviderType);
        Assert.False(selection.Value.DomainMatched);
    }

    [Fact]
    public void Select_PriorityTie_SelectsHighestVersion()
    {
        var versionOne = BuildConfig(ProviderType.AzureAd, priority: 1);                 // Version 1
        var versionTwo = BuildConfig(ProviderType.Keycloak, priority: 1, bumpVersion: 1); // Version 2

        var selection = IdpConfigurationSelector.Select(
            new[] { versionOne, versionTwo }, systemSuiteId: null, emailDomain: null, providerType: null);

        Assert.NotNull(selection);
        Assert.Equal(ProviderType.Keycloak, selection!.Value.Configuration.ProviderType);
    }

    // ── Filtro por suite ─────────────────────────────────────────────────────────

    [Fact]
    public void Select_SuiteFilter_OnlyConsidersMatchingSuite()
    {
        var suiteA = Guid.NewGuid();
        var suiteB = Guid.NewGuid();
        var inSuiteA = BuildConfig(ProviderType.AzureAd, priority: 1, suiteId: suiteA);
        var inSuiteB = BuildConfig(ProviderType.Keycloak, priority: 99, suiteId: suiteB);

        var selection = IdpConfigurationSelector.Select(
            new[] { inSuiteA, inSuiteB }, systemSuiteId: suiteB, emailDomain: null, providerType: null);

        Assert.NotNull(selection);
        // La de suiteA tiene mejor prioridad pero se filtra fuera; gana la única de suiteB.
        Assert.Equal(ProviderType.Keycloak, selection!.Value.Configuration.ProviderType);
    }

    [Fact]
    public void Select_SuiteFilter_WhenNoMatch_ReturnsNull()
    {
        var config = BuildConfig(ProviderType.AzureAd, priority: 1, suiteId: Guid.NewGuid());

        var selection = IdpConfigurationSelector.Select(
            new[] { config }, systemSuiteId: Guid.NewGuid(), emailDomain: null, providerType: null);

        Assert.Null(selection);
    }

    // ── Routing por dominio ──────────────────────────────────────────────────────

    [Fact]
    public void Select_DomainMatch_PrefersDomainMatchedOverPriority()
    {
        var domainConfig = BuildConfig(ProviderType.AzureAd, priority: 10, domainHints: new[] { "acme.com" });
        var betterPriority = BuildConfig(ProviderType.Keycloak, priority: 1);

        var selection = IdpConfigurationSelector.Select(
            new[] { betterPriority, domainConfig }, systemSuiteId: null, emailDomain: "user@acme.com", providerType: null);

        Assert.NotNull(selection);
        Assert.Equal(ProviderType.AzureAd, selection!.Value.Configuration.ProviderType);
        Assert.True(selection.Value.DomainMatched);
    }

    [Fact]
    public void Select_WhenDomainMatchesNoHint_FallsBackToPrioritySelection()
    {
        var domainConfig = BuildConfig(ProviderType.AzureAd, priority: 10, domainHints: new[] { "acme.com" });
        var betterPriority = BuildConfig(ProviderType.Keycloak, priority: 1);

        var selection = IdpConfigurationSelector.Select(
            new[] { betterPriority, domainConfig }, systemSuiteId: null, emailDomain: "user@other.com", providerType: null);

        Assert.NotNull(selection);
        Assert.Equal(ProviderType.Keycloak, selection!.Value.Configuration.ProviderType);
        Assert.False(selection.Value.DomainMatched);
    }

    // ── Filtro por tipo de proveedor ─────────────────────────────────────────────

    [Fact]
    public void Select_ProviderTypeFilter_OnlyConsidersMatchingType()
    {
        var azure = BuildConfig(ProviderType.AzureAd, priority: 1);
        var keycloak = BuildConfig(ProviderType.Keycloak, priority: 99);

        var selection = IdpConfigurationSelector.Select(
            new[] { azure, keycloak }, systemSuiteId: null, emailDomain: null, providerType: ProviderType.Keycloak.Name);

        Assert.NotNull(selection);
        Assert.Equal(ProviderType.Keycloak, selection!.Value.Configuration.ProviderType);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static IdpConfigurationAggregate BuildConfig(
        ProviderType providerType,
        int priority,
        Guid? suiteId = null,
        string[]? domainHints = null,
        int bumpVersion = 0,
        bool activate = true)
    {
        var config = IdpConfigurationAggregate.Create(
            TenantId.Load(TenantGuid),
            suiteId.HasValue ? SystemSuiteId.Load(suiteId.Value) : SystemSuiteId.Create(),
            providerType,
            domainHints ?? Array.Empty<string>(),
            "{\"issuer\":\"https://idp.example\"}",
            "vault/secret/idp",
            priority,
            null,
            Actor).Value;

        // Update() incrementa Version y solo se permite en Draft/Inactive: se usa para el desempate.
        for (var i = 0; i < bumpVersion; i++)
        {
            config.Update("{\"issuer\":\"https://idp.example/v\"}", "vault/secret/idp", domainHints ?? Array.Empty<string>(), Actor);
        }

        if (activate)
        {
            config.Activate(Actor);
        }

        return config;
    }
}
