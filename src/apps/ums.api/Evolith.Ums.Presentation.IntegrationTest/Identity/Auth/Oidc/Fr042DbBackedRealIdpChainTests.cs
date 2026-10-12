using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;
using Evolith.Ums.Infrastructure.Persistence.Configuration;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity.Auth.Oidc;

/// <summary>
/// G-109 (residual de FR-042, ADR-UMS-097): E2E de resolución/fallback FR-042 <b>respaldado por BD</b>,
/// juntando los dos mundos que hasta ahora vivían aparte —el arnés Keycloak real (protocolo OIDC) y el
/// camino de producción de resolución por reglas (<c>IdpConfigurationSelector</c> →
/// <c>IdpConfigurationOidcProviderConfigStore</c> → adaptador OIDC → <c>IdpChainAuthenticator</c>)—
/// contra DOS contenedores reales (Postgres + Keycloak).
///
/// <para>A diferencia del <c>KeycloakOidcRealHarnessTests</c> (que usa un <c>StaticOidcProviderConfigStore</c>
/// y no ejercita la resolución), aquí el store REAL lee las <c>IdpConfiguration</c> sembradas en Postgres,
/// y sus endpoints apuntan al issuer/endpoints REALES del Keycloak del contenedor —tomados de
/// <see cref="KeycloakContainerFixture.Endpoints"/>/<see cref="KeycloakContainerFixture.RealmIssuer"/>,
/// nunca hardcodeados—.</para>
///
/// <para><b>Escenarios (por valor de seguridad):</b></para>
/// <list type="number">
///   <item><b>Camino feliz</b>: una <c>IdpConfiguration</c> real resuelta por reglas autentica contra el
///     Keycloak real (Authorization Code + PKCE) por la ruta real del <c>IdpChainAuthenticator</c>.</item>
///   <item><b>Resolución por reglas</b> (<c>AuthMethodResolverService</c>) sobre configs reales de BD →
///     proveedor Keycloak del inquilino.</item>
///   <item><b>Fallback por indisponibilidad</b>: primaria a endpoint muerto (AUTH_035 infra) → la cadena
///     avanza a la secundaria (Keycloak real) y el login procede.</item>
///   <item><b>Invariante anti credential-spraying</b>: primaria = Keycloak real que RECHAZA la credencial
///     (400 <c>invalid_grant</c> → AUTH_021) es TERMINAL — la cadena NO avanza a la secundaria.</item>
///   <item><b>Ciclo</b> con todos los eslabones indisponibles → cortado → 503 (AUTH_018).</item>
/// </list>
///
/// El único doble de test es <see cref="SingleAdapterIdpAuthStrategy"/>: enruta cualquier estrategia OIDC
/// al <see cref="OidcIdpAuthAdapter"/> REAL, igual que hace <c>IdpAuthAdapterFactorySetup</c> (Shell.Factory)
/// en producción para Keycloak/GenericOidc/…; evita cablear la fábrica sin cambiar el camino real
/// (store → adaptador → token client → validador). No se toca producción (SD-05).
/// </summary>
[Collection("PostgresKeycloak")]
public sealed class Fr042DbBackedRealIdpChainTests
{
    private const string DeadIssuerPrimary = "http://127.0.0.1:1/realms/dead-primary";
    private const string DeadIssuerSecondary = "http://127.0.0.1:1/realms/dead-secondary";
    private static readonly ActorId Actor = ActorId.Create("00000000-0000-0000-0000-000000000109");

    private readonly PostgresKeycloakFixture _fixture;
    private readonly KeycloakContainerFixture _keycloak;

    public Fr042DbBackedRealIdpChainTests(PostgresKeycloakFixture fixture)
    {
        _fixture = fixture;
        _keycloak = fixture.Keycloak;
    }

    // ── (1) Camino feliz: resolución por reglas desde BD + auth contra Keycloak real ────────────────

    [Fact]
    public async Task HappyPath_SingleRealKeycloakConfigFromDb_AuthenticatesViaRealChain()
    {
        if (!_fixture.BothAvailable)
        {
            Assert.Skip($"Postgres+Keycloak requeridos. {_fixture.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        // Se siembra en Postgres UNA IdpConfiguration Keycloak activa apuntando al Keycloak REAL.
        await SeedConfigAsync(NewConfig(tenantId, ProviderType.Keycloak, _keycloak.RealmIssuer, priority: 1), ct);

        // Login federado real (Authorization Code + PKCE headless) → callback con code/state/nonce reales.
        var callbackJson = await ObtainRealCallbackJsonAsync(BuildRealKeycloakConfig(), ct);

        // Inquilino con proveedor Keycloak activo (el puente reglas↔dominio lo requiere).
        var tenant = BuildTenant(active: IdpStrategy.Keycloak, tenantId);

        await using var chainDb = CreateDbContext();
        await using var storeDb = CreateDbContext();
        using var tokenHttp = NewHttpClient();
        using var jwksHttp = NewHttpClient();
        var audit = new Mock<IAuthAuditService>();
        var chain = BuildChain(chainDb, storeDb, tokenHttp, jwksHttp, audit.Object);

        var result = await chain.AuthenticateAsync(tenant, callbackJson, systemSuiteId: null, emailDomain: null, "10.0.0.1", ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Identity.Email.Should().Be(KeycloakContainerFixture.TestEmail);
        result.Value.Identity.ExternalId.Should().NotBeNullOrWhiteSpace("el 'sub' real de Keycloak mapea a ExternalId");
        result.Value.Provider.Strategy.Id.Should().Be(IdpStrategy.Keycloak.Id);
        audit.Verify(evt => evt.RecordAuthEventAsync(It.IsAny<AuthAuditEvent>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    // ── (2) Resolución por reglas: AuthMethodResolverService sobre configs reales de BD ─────────────

    [Fact]
    public async Task AuthMethodResolver_ResolvesRealKeycloakProviderByRules_FromDb()
    {
        if (!_fixture.PostgresAvailable)
        {
            Assert.Skip($"Postgres requerido. {_fixture.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        // Dos configs activas en BD: Keycloak (prioridad 1, gana) y AzureAd (prioridad 10). El selector
        // elige por prioridad y el resolver la reconcilia con el proveedor Keycloak activo del inquilino.
        await SeedConfigAsync(NewConfig(tenantId, ProviderType.Keycloak, _keycloak.IsAvailable ? _keycloak.RealmIssuer : DeadIssuerPrimary, priority: 1), ct);
        await SeedConfigAsync(NewConfig(tenantId, ProviderType.AzureAd, DeadIssuerSecondary, priority: 10), ct);

        var tenant = BuildTenant(active: IdpStrategy.Keycloak, tenantId);

        var config = new Mock<IConfigurationProvider>();
        config.Setup(c => c.GetWithPrecedence(AppConfigurationCodes.AuthUseExternalIdp, tenantId, It.IsAny<Guid?>(), It.IsAny<Guid?>()))
              .Returns(BuildUseExternalIdp(tenantId, true));
        var tenantRepo = new Mock<Evolith.Ums.Domain.Identity.ITenantRepository>();
        tenantRepo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        await using var db = CreateDbContext();
        var resolver = new AuthMethodResolverService(config.Object, tenantRepo.Object, new PostgreSqlIdpConfigurationRepository(db));

        var result = await resolver.ResolveAsync(tenantId, AuthAccessScope.ExternalApi, systemSuiteId: null, emailDomain: null, ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Type.Should().Be(AuthMethodType.IDP);
        result.Value.Provider.Should().NotBeNull();
        result.Value.Provider!.Strategy.Id.Should().Be(IdpStrategy.Keycloak.Id, "la regla FR-042 gana por prioridad y se reconcilia con el proveedor activo");
    }

    // ── (3) Fallback por indisponibilidad: primaria muerta → avanza a Keycloak real ─────────────────

    [Fact]
    public async Task Fallback_PrimaryInfraDown_AdvancesToRealKeycloak_Succeeds()
    {
        if (!_fixture.BothAvailable)
        {
            Assert.Skip($"Postgres+Keycloak requeridos. {_fixture.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        // Secundaria (Keycloak real) se crea primero para conocer su Id y enlazar la primaria a ella.
        var secondary = NewConfig(tenantId, ProviderType.Keycloak, _keycloak.RealmIssuer, priority: 2);
        var primary = NewConfig(tenantId, ProviderType.GenericOidc, DeadIssuerPrimary, priority: 1, fallbackToId: secondary.GetId().GetValue());
        await SeedConfigAsync(secondary, ct);
        await SeedConfigAsync(primary, ct);

        // La credencial (callback) se emite en un login real contra el Keycloak real (la secundaria).
        var callbackJson = await ObtainRealCallbackJsonAsync(BuildRealKeycloakConfig(), ct);

        // El inquilino registra AMBAS estrategias: GenericOidc (primaria) y Keycloak (secundaria).
        var tenant = BuildTenant(active: IdpStrategy.Keycloak, tenantId, alsoRegister: IdpStrategy.GenericOidc);

        await using var chainDb = CreateDbContext();
        await using var storeDb = CreateDbContext();
        using var tokenHttp = NewHttpClient();
        using var jwksHttp = NewHttpClient();
        var audit = new Mock<IAuthAuditService>();
        var chain = BuildChain(chainDb, storeDb, tokenHttp, jwksHttp, audit.Object);

        var result = await chain.AuthenticateAsync(tenant, callbackJson, systemSuiteId: null, emailDomain: null, "10.0.0.1", ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Identity.Email.Should().Be(KeycloakContainerFixture.TestEmail);
        // Autenticó la SECUNDARIA (Keycloak), no la primaria muerta (GenericOidc).
        result.Value.Provider.Strategy.Id.Should().Be(IdpStrategy.Keycloak.Id);
        // Un evento por proveedor intentado: primaria (infra→advance) + secundaria (success).
        audit.Verify(evt => evt.RecordAuthEventAsync(It.IsAny<AuthAuditEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── (4) Invariante anti credential-spraying: rechazo real de credencial es TERMINAL ─────────────

    [Fact]
    public async Task CredentialRejectedByRealKeycloak_IsTerminal_ChainDoesNotAdvance()
    {
        if (!_fixture.BothAvailable)
        {
            Assert.Skip($"Postgres+Keycloak requeridos. {_fixture.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        // Primaria = Keycloak REAL (prioridad 1). Secundaria = endpoint muerto (prioridad 2). Si —y solo
        // si— la cadena avanzara indebidamente tras un rechazo de credencial, caería en la secundaria
        // muerta (infra) y agotaría la cadena → AUTH_018. El rechazo terminal debe cortar ANTES: AUTH_021.
        var secondary = NewConfig(tenantId, ProviderType.GenericOidc, DeadIssuerSecondary, priority: 2);
        var primary = NewConfig(tenantId, ProviderType.Keycloak, _keycloak.RealmIssuer, priority: 1, fallbackToId: secondary.GetId().GetValue());
        await SeedConfigAsync(secondary, ct);
        await SeedConfigAsync(primary, ct);

        // Callback con code REAL emitido por Keycloak pero MANIPULADO: el token endpoint real lo rechaza
        // con 400 invalid_grant (rechazo de credencial/petición) → AUTH_021 (terminal), no 5xx (infra).
        var callbackJson = await ObtainRealCallbackJsonAsync(BuildRealKeycloakConfig(), ct, TamperCode);

        var tenant = BuildTenant(active: IdpStrategy.Keycloak, tenantId, alsoRegister: IdpStrategy.GenericOidc);

        await using var chainDb = CreateDbContext();
        await using var storeDb = CreateDbContext();
        using var tokenHttp = NewHttpClient();
        using var jwksHttp = NewHttpClient();
        var audit = new Mock<IAuthAuditService>();
        var chain = BuildChain(chainDb, storeDb, tokenHttp, jwksHttp, audit.Object);

        var result = await chain.AuthenticateAsync(tenant, callbackJson, systemSuiteId: null, emailDomain: null, "10.0.0.1", ct);

        result.IsFailure.Should().BeTrue("un rechazo real de credencial por Keycloak (400 invalid_grant) es terminal");
        result.Error.Should().Contain("AUTH_021", "el token endpoint real devolvió 4xx → clasificación terminal");
        result.Error.Should().NotContain("AUTH_018", "TERMINAL: la cadena NO avanzó a la secundaria (anti credential-spraying, ADR-UMS-097 §2.3)");
        // Solo el intento primario se auditó: la secundaria nunca se tocó.
        audit.Verify(evt => evt.RecordAuthEventAsync(It.IsAny<AuthAuditEvent>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    // ── (5) Ciclo con todos los eslabones indisponibles → cortado → 503 (opcional, barato) ──────────

    [Fact]
    public async Task Cycle_AllInfraUnavailable_Detected_Returns503()
    {
        if (!_fixture.PostgresAvailable)
        {
            Assert.Skip($"Postgres requerido. {_fixture.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        // A (GenericOidc, muerto) → B (Keycloak-typed, muerto) → A. Ambos endpoints cerrados ⇒ AUTH_035
        // (infra) en cada intento ⇒ la cadena avanza hasta detectar el ciclo y se corta con 503.
        var a = NewConfig(tenantId, ProviderType.GenericOidc, DeadIssuerPrimary, priority: 1);
        var b = NewConfig(tenantId, ProviderType.Keycloak, DeadIssuerSecondary, priority: 2, fallbackToId: a.GetId().GetValue());
        a.Props.FallbackToId = b.GetId().GetValue(); // cierra el ciclo A→B→A
        await SeedConfigAsync(a, ct);
        await SeedConfigAsync(b, ct);

        // Callback bien formado (state==expectedState): la resolución de config y la comprobación de
        // 'state' pasan; el intercambio de código muere en transporte (endpoint cerrado) → infra.
        var callbackJson = BuildWellFormedButUnusedCallback();

        var tenant = BuildTenant(active: IdpStrategy.Keycloak, tenantId, alsoRegister: IdpStrategy.GenericOidc);

        await using var chainDb = CreateDbContext();
        await using var storeDb = CreateDbContext();
        using var tokenHttp = NewHttpClient();
        using var jwksHttp = NewHttpClient();
        var audit = new Mock<IAuthAuditService>();
        var chain = BuildChain(chainDb, storeDb, tokenHttp, jwksHttp, audit.Object);

        var result = await chain.AuthenticateAsync(tenant, callbackJson, systemSuiteId: null, emailDomain: null, "10.0.0.1", ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AUTH_018", "cadena agotada por indisponibilidad + ciclo → 503, no 401");
        // A y B intentados una vez cada uno; sin bucle infinito.
        audit.Verify(evt => evt.RecordAuthEventAsync(It.IsAny<AuthAuditEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── Composición del camino REAL (store → adaptador → token client → validador → cadena) ─────────

    private static IdpChainAuthenticator BuildChain(
        UmsPlatformDbContext chainDb,
        UmsPlatformDbContext storeDb,
        HttpClient tokenHttp,
        HttpClient jwksHttp,
        IAuthAuditService audit)
    {
        // Store REAL: lee las IdpConfiguration del inquilino de Postgres y parsea sus endpoints.
        var store = new IdpConfigurationOidcProviderConfigStore(new PostgreSqlIdpConfigurationRepository(storeDb));
        // Adaptador OIDC REAL (Authorization Code + PKCE) con token client + validador reales.
        var adapter = new OidcIdpAuthAdapter(
            store,
            new HttpOidcTokenClient(tokenHttp),
            new OidcIdTokenValidator(new HttpJwksProvider(jwksHttp), TimeProvider.System));
        // Estrategia = enruta al adaptador OIDC real (mismo destino que la Shell.Factory de producción).
        var strategy = new SingleAdapterIdpAuthStrategy(adapter);

        // Tope de saltos: default seguro (no configurado ⇒ el proveedor devuelve el default pasado).
        var config = new Mock<IConfigurationProvider>();
        config.Setup(c => c.GetValueAs<int>(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int>()))
              .Returns<string, Guid?, int>((_, _, def) => def);

        return new IdpChainAuthenticator(new PostgreSqlIdpConfigurationRepository(chainDb), strategy, audit, config.Object);
    }

    // ── Siembra y lectura sobre el Postgres REAL ────────────────────────────────────────────────────

    private async Task SeedConfigAsync(IdpConfigurationAggregate config, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        var repo = new PostgreSqlIdpConfigurationRepository(db);
        await repo.AddAsync(config, ct);
        await repo.SaveChangesAsync(ct);
    }

    private UmsPlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseNpgsql(_fixture.ConnectionString, sql => sql.EnableRetryOnFailure(3))
            .Options;
        // Contexto de sistema (org nula) ⇒ sin filtro global por inquilino: siembra y lectura ven la fila.
        return new UmsPlatformDbContext(
            options,
            new SystemTenantContext(),
            new Mock<MediatR.IPublisher>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    private static IdpConfigurationAggregate NewConfig(
        Guid tenantId, ProviderType providerType, string issuer, int priority, Guid? fallbackToId = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            issuer,
            client_id = KeycloakContainerFixture.ClientId,
            redirect_uri = KeycloakContainerFixture.RedirectUri,
            scope = "openid email profile",
        });

        var config = IdpConfigurationAggregate.Create(
            TenantId.Load(tenantId),
            SystemSuiteId.Create(),
            providerType,
            Array.Empty<string>(),
            payload,
            "vault/secret/idp",
            priority,
            fallbackToId,
            Actor).Value;
        config.Activate(Actor);
        return config;
    }

    // ── Inquilino con proveedores registrados (puente reglas↔dominio por Id de estrategia) ──────────

    private static TenantAggregate BuildTenant(IdpStrategy active, Guid tenantId, params IdpStrategy[] alsoRegister)
    {
        var tenant = TenantAggregate.Create(
            Code.Create("RANSA"),
            Name.Create("Ransa"),
            OrganizationType.INTERNAL,
            Actor,
            active,
            tenantId: TenantId.Load(tenantId)).Value;

        tenant.RegisterIdentityProvider(
            Code.Create(active.Name.ToUpperInvariant()), Name.Create(active.Name), Description.Create(string.Empty), active, Actor);

        foreach (var strategy in alsoRegister)
        {
            tenant.RegisterIdentityProvider(
                Code.Create(strategy.Name.ToUpperInvariant()), Name.Create(strategy.Name), Description.Create(string.Empty), strategy, Actor);
        }

        // Se activa el proveedor primario; los demás quedan registrados+inactivos (la cadena 2b los usa
        // igualmente porque la gobierna IdpConfiguration.Status, no el flag de proveedor activo, §2.5).
        tenant.ActivateIdentityProvider(tenant.IdentityProviders.First().GetId(), Actor);
        tenant.DomainEvents.MarkChangesAsCommitted();
        return tenant;
    }

    private OidcProviderConfig BuildRealKeycloakConfig()
        => new(
            _keycloak.Endpoints,
            new OidcClientSettings(
                KeycloakContainerFixture.ClientId,
                ClientSecret: null,           // cliente público + PKCE
                KeycloakContainerFixture.RedirectUri,
                "openid email profile"));

    private static AppConfigurationAggregate BuildUseExternalIdp(Guid tenantId, bool value)
        => AppConfigurationAggregate.Create(
            TenantId.Load(tenantId), null, null,
            Code.Create(AppConfigurationCodes.AuthUseExternalIdp),
            ConfigurationValue.Create(value.ToString().ToLowerInvariant()),
            Description.Create("Use external IDP"),
            true, false, Actor).Value;

    // ── Login headless (Authorization Code + PKCE) contra el Keycloak real ──────────────────────────

    /// <summary>
    /// Ejerce el login real (GET del formulario → POST de credenciales → 302 con <c>?code=&amp;state=</c>)
    /// y empaqueta el callback como JSON tal cual lo consume el adaptador. Un <paramref name="codeMutator"/>
    /// opcional permite manipular el code (para forzar un rechazo real de credencial, escenario 4).
    /// </summary>
    private async Task<string> ObtainRealCallbackJsonAsync(
        OidcProviderConfig config, CancellationToken ct, Func<string, string>? codeMutator = null)
    {
        var authRequest = new OidcAuthorizationRequestFactory().Build(config);
        var (code, state) = await PerformHeadlessLoginAsync(authRequest.AuthorizationUrl, ct);
        state.Should().Be(authRequest.State, "Keycloak devuelve el mismo 'state' emitido");

        return JsonSerializer.Serialize(new
        {
            code = codeMutator?.Invoke(code) ?? code,
            state,
            expectedState = authRequest.State,
            expectedNonce = authRequest.Nonce,
            codeVerifier = authRequest.CodeVerifier,
            redirectUri = KeycloakContainerFixture.RedirectUri,
        });
    }

    /// <summary>Callback bien formado con state==expectedState pero sin login real (para eslabones muertos).</summary>
    private static string BuildWellFormedButUnusedCallback()
    {
        var opaque = Guid.NewGuid().ToString("N");
        return JsonSerializer.Serialize(new
        {
            code = "unused-code-" + opaque,
            state = opaque,
            expectedState = opaque,
            expectedNonce = Guid.NewGuid().ToString("N"),
            codeVerifier = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            redirectUri = KeycloakContainerFixture.RedirectUri,
        });
    }

    /// <summary>Manipula el code preservando el formato: Keycloak lo rechaza con 400 invalid_grant.</summary>
    private static string TamperCode(string code)
    {
        var chars = code.ToCharArray();
        chars[0] = chars[0] == 'A' ? 'B' : 'A';
        return new string(chars);
    }

    private static async Task<(string Code, string State)> PerformHeadlessLoginAsync(
        string authorizationUrl, CancellationToken ct)
    {
        // Manejo manual de cookies: la CookieContainer de .NET descarta las cookies de sesión de Keycloak
        // (SameSite=None sobre HTTP), lo que hace que el POST de login sea rechazado. Se reenvían a mano.
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var http = new HttpClient(handler);

        using var pageResponse = await http.GetAsync(authorizationUrl, ct);
        pageResponse.IsSuccessStatusCode.Should().BeTrue("el endpoint de autorización debe servir el formulario de login");
        var html = await pageResponse.Content.ReadAsStringAsync(ct);
        var cookieHeader = BuildCookieHeader(pageResponse);

        var formAction = ExtractLoginFormAction(html);
        formAction.Should().NotBeNullOrWhiteSpace("el formulario de login de Keycloak debe exponer su 'action'");

        var loginForm = new Dictionary<string, string>
        {
            ["username"] = KeycloakContainerFixture.TestUsername,
            ["password"] = KeycloakContainerFixture.TestPassword,
            ["credentialId"] = string.Empty,
        };

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, formAction)
        {
            Content = new FormUrlEncodedContent(loginForm),
        };
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            loginRequest.Headers.Add("Cookie", cookieHeader);
        }

        using var loginResponse = await http.SendAsync(loginRequest, ct);
        if (loginResponse.StatusCode is not (HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.Redirect))
        {
            var body = await loginResponse.Content.ReadAsStringAsync(ct);
            var snippet = body.Length > 600 ? body[..600] : body;
            throw new Xunit.Sdk.XunitException(
                $"Login POST no redirigió. Status={(int)loginResponse.StatusCode}. Action={formAction}. Body:\n{snippet}");
        }

        var location = loginResponse.Headers.Location;
        location.Should().NotBeNull("Keycloak debe devolver Location con el redirect_uri + code");

        var query = ParseQuery(location!);
        query.Should().ContainKey("code");
        query.Should().ContainKey("state");
        return (query["code"], query["state"]);
    }

    private static string BuildCookieHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return string.Empty;
        }

        var pairs = setCookies
            .Select(sc => sc.Split(';', 2)[0].Trim())
            .Where(pair => pair.Length > 0 && pair.Contains('='));

        return string.Join("; ", pairs);
    }

    private static string ExtractLoginFormAction(string html)
    {
        var login = Regex.Match(
            html, "id=\"kc-form-login\"[^>]*action=\"(?<url>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var raw = login.Success
            ? login.Groups["url"].Value
            : Regex.Match(html, "<form[^>]*action=\"(?<url>[^\"]+)\"[^>]*method=\"post\"",
                RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups["url"].Value;

        return WebUtility.HtmlDecode(raw);
    }

    private static Dictionary<string, string> ParseQuery(Uri location)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in location.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            result[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
        }

        return result;
    }

    private static HttpClient NewHttpClient() => new() { Timeout = TimeSpan.FromSeconds(30) };

    // ── Dobles de test (mínimos, documentados) ──────────────────────────────────────────────────────

    /// <summary>
    /// Único doble del test: enruta cualquier estrategia al <see cref="OidcIdpAuthAdapter"/> REAL, tal como
    /// hace <c>IdpAuthAdapterFactorySetup</c> (Shell.Factory) en producción para las estrategias OIDC.
    /// Evita cablear la fábrica DI sin desviarse del camino real (store → adaptador → token client → validador).
    /// </summary>
    private sealed class SingleAdapterIdpAuthStrategy : IIdpAuthStrategy
    {
        private readonly OidcIdpAuthAdapter _adapter;

        public SingleAdapterIdpAuthStrategy(OidcIdpAuthAdapter adapter) => _adapter = adapter;

        public Task<Result<ExternalIdentity>> AuthenticateAsync(
            Guid tenantId, string credential, IdentityProvider provider, CancellationToken cancellationToken = default)
            => _adapter.ValidateAsync(provider, credential, cancellationToken);
    }

    /// <summary>Contexto de inquilino de sistema (org nula ⇒ sin filtro global por inquilino).</summary>
    private sealed class SystemTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public Guid? OriginalTenantId => null;
        public bool IsInternalAdmin => true;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
