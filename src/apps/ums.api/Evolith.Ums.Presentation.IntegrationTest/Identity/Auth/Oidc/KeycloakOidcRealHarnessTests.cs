using System.Net.Http;
using System.Text.RegularExpressions;
using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity.Auth.Oidc;

/// <summary>
/// G-109 (deuda de ADR-UMS-094): arnés de integración que ejerce el adaptador OIDC federado
/// contra un <b>Keycloak real</b> (Testcontainers), sin mocks del token endpoint ni del JWKS.
///
/// Los tests unitarios (Application.Test/.../Oidc) validan el <c>id_token</c> con JWKS/token
/// mockeados e issuer ficticio <c>idp.test</c>. Aquí lo esencial es lo que esos mocks NO cubren:
/// que un <c>id_token</c> emitido y firmado por llaves RSA reales de Keycloak pase por la ruta
/// de validación real del adaptador — <see cref="HttpJwksProvider"/> contra el JWKS real +
/// <see cref="OidcIdTokenValidator"/> (firma RS256, iss/aud/exp/nonce).
///
/// Cobertura por test:
/// <list type="bullet">
///   <item><b>ESENCIAL</b> (Direct Access Grant): id_token real → JWKS real → validador real.
///     Positivo (firma/iss/aud reales) y negativos con llaves reales (aud, firma manipulada, iss).</item>
///   <item><b>DESEABLE</b> (authorization_code + PKCE headless): flujo completo a través de
///     <see cref="OidcIdpAuthAdapter"/> con <see cref="HttpOidcTokenClient"/> real (intercambio de
///     código + PKCE) — incluye validación de <c>nonce</c> real emitido en la autorización.</item>
///   <item><b>OPCIONAL</b> (resolución por configuración, estilo FR-042): endpoints derivados por
///     <see cref="OidcProviderConfigParser"/> desde un <c>ConfigPayload</c> con solo el issuer del
///     Keycloak del contenedor, y validación de un id_token real contra ellos.</item>
/// </list>
/// </summary>
[Collection("Keycloak")]
public sealed class KeycloakOidcRealHarnessTests
{
    private readonly KeycloakContainerFixture _keycloak;

    public KeycloakOidcRealHarnessTests(KeycloakContainerFixture keycloak) => _keycloak = keycloak;

    // ── ESENCIAL: id_token REAL por la ruta de validación REAL (JWKS + validador) ──────────────

    [Fact]
    public async Task RealIdToken_ViaDirectAccessGrant_PassesRealJwksAndValidator()
    {
        if (!_keycloak.IsAvailable)
        {
            Assert.Skip($"Keycloak/Docker requerido. {_keycloak.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;

        using var http = new HttpClient();
        var idToken = await RequestIdTokenViaDirectAccessGrantAsync(http, ct);
        idToken.Should().NotBeNullOrWhiteSpace("Keycloak debe emitir un id_token real para el password grant con scope openid");

        // Ruta de validación REAL: HttpJwksProvider hace GET al JWKS real de Keycloak;
        // OidcIdTokenValidator verifica firma RS256 contra las llaves reales + iss/aud/exp.
        using var jwksHttp = new HttpClient();
        var validator = new OidcIdTokenValidator(new HttpJwksProvider(jwksHttp), TimeProvider.System);

        var result = await validator.ValidateAsync(
            idToken!,
            _keycloak.Endpoints,
            expectedAudience: KeycloakContainerFixture.ClientId,
            // Direct Access Grant no emite 'nonce' (solo el authorization endpoint lo hace):
            // el validador omite la comprobación de nonce cuando expectedNonce es vacío.
            expectedNonce: string.Empty,
            ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Email.Should().Be(KeycloakContainerFixture.TestEmail);
        result.Value.ExternalId.Should().NotBeNullOrWhiteSpace("el 'sub' real de Keycloak mapea a ExternalId");
        result.Value.Claims.Should().ContainKey("preferred_username");
    }

    [Fact]
    public async Task RealIdToken_WrongAudience_RejectedByRealValidator_AUTH028()
    {
        if (!_keycloak.IsAvailable)
        {
            Assert.Skip($"Keycloak/Docker requerido. {_keycloak.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var http = new HttpClient();
        var idToken = await RequestIdTokenViaDirectAccessGrantAsync(http, ct);

        using var jwksHttp = new HttpClient();
        var validator = new OidcIdTokenValidator(new HttpJwksProvider(jwksHttp), TimeProvider.System);

        // Firma/JWKS reales, pero exigimos una audiencia distinta a la del token real ⇒ rechazo.
        var result = await validator.ValidateAsync(
            idToken!, _keycloak.Endpoints, expectedAudience: "otro-cliente", expectedNonce: string.Empty, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AUTH_028");
    }

    [Fact]
    public async Task RealIdToken_TamperedSignature_RejectedByRealValidator_AUTH026()
    {
        if (!_keycloak.IsAvailable)
        {
            Assert.Skip($"Keycloak/Docker requerido. {_keycloak.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var http = new HttpClient();
        var idToken = await RequestIdTokenViaDirectAccessGrantAsync(http, ct);

        // Manipula la firma (último segmento) preservando el formato JWT: el validador debe
        // rechazarla al verificar contra las llaves públicas reales del JWKS.
        var parts = idToken!.Split('.');
        var sig = parts[2].ToCharArray();
        sig[0] = sig[0] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}.{parts[1]}.{new string(sig)}";

        using var jwksHttp = new HttpClient();
        var validator = new OidcIdTokenValidator(new HttpJwksProvider(jwksHttp), TimeProvider.System);

        var result = await validator.ValidateAsync(
            tampered, _keycloak.Endpoints, expectedAudience: KeycloakContainerFixture.ClientId,
            expectedNonce: string.Empty, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AUTH_026");
    }

    [Fact]
    public async Task RealIdToken_WrongIssuer_RejectedByRealValidator_AUTH027()
    {
        if (!_keycloak.IsAvailable)
        {
            Assert.Skip($"Keycloak/Docker requerido. {_keycloak.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var http = new HttpClient();
        var idToken = await RequestIdTokenViaDirectAccessGrantAsync(http, ct);

        using var jwksHttp = new HttpClient();
        var validator = new OidcIdTokenValidator(new HttpJwksProvider(jwksHttp), TimeProvider.System);

        // JWKS real (mismo host), pero declaramos un issuer esperado distinto ⇒ rechazo por iss.
        var endpointsWithWrongIssuer = _keycloak.Endpoints with { Issuer = "https://malicioso.test/realms/evolith" };

        var result = await validator.ValidateAsync(
            idToken!, endpointsWithWrongIssuer, expectedAudience: KeycloakContainerFixture.ClientId,
            expectedNonce: string.Empty, ct);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AUTH_027");
    }

    // ── DESEABLE: flujo authorization_code + PKCE headless por el adaptador completo ─────────────

    [Fact]
    public async Task FullAuthorizationCodePkceFlow_ThroughAdapter_ReturnsExternalIdentity()
    {
        if (!_keycloak.IsAvailable)
        {
            Assert.Skip($"Keycloak/Docker requerido. {_keycloak.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;

        var config = new OidcProviderConfig(
            _keycloak.Endpoints,
            new OidcClientSettings(
                ClientId: KeycloakContainerFixture.ClientId,
                ClientSecret: null, // cliente público + PKCE
                RedirectUri: KeycloakContainerFixture.RedirectUri,
                Scopes: "openid email profile"));

        // 1) El adaptador construye la URL de autorización (state/nonce/code_verifier/code_challenge S256).
        var authRequest = new OidcAuthorizationRequestFactory().Build(config);

        // 2) Login headless contra Keycloak real: GET del form → POST credenciales → 302 con ?code=&state=.
        var (code, returnedState) = await PerformHeadlessAuthorizationCodeLoginAsync(authRequest.AuthorizationUrl, ct);
        returnedState.Should().Be(authRequest.State, "Keycloak devuelve el mismo 'state' emitido");

        // 3) El nivel de presentación empaqueta el callback como JSON; el adaptador lo intercambia por
        //    tokens (HttpOidcTokenClient real, grant_type=authorization_code + code_verifier) y valida
        //    el id_token real (JWKS real + nonce real emitido en la autorización).
        var callbackJson = JsonSerializer.Serialize(new
        {
            code,
            state = returnedState,
            expectedState = authRequest.State,
            expectedNonce = authRequest.Nonce,
            codeVerifier = authRequest.CodeVerifier,
            redirectUri = KeycloakContainerFixture.RedirectUri,
        });

        using var tokenHttp = new HttpClient();
        using var jwksHttp = new HttpClient();
        var adapter = new OidcIdpAuthAdapter(
            new StaticOidcProviderConfigStore(config),
            new HttpOidcTokenClient(tokenHttp),
            new OidcIdTokenValidator(new HttpJwksProvider(jwksHttp), TimeProvider.System));

        var result = await adapter.ValidateAsync(BuildKeycloakProvider(), callbackJson, ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Email.Should().Be(KeycloakContainerFixture.TestEmail);
        result.Value.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    // ── OPCIONAL: resolución por configuración (parser) contra el Keycloak real (estilo FR-042) ──

    [Fact]
    public async Task ConfigPayloadIssuerOnly_ResolvesRealKeycloakEndpoints_AndValidatesRealToken()
    {
        if (!_keycloak.IsAvailable)
        {
            Assert.Skip($"Keycloak/Docker requerido. {_keycloak.StartupError}");
            return;
        }

        var ct = TestContext.Current.CancellationToken;

        // ConfigPayload con SOLO el issuer del contenedor: el parser deriva auth/token/jwks al estilo
        // Keycloak/OIDC (como lo haría IdpConfiguration.ConfigPayload en runtime, sin hardcodear endpoints).
        var configPayload = JsonSerializer.Serialize(new
        {
            issuer = _keycloak.RealmIssuer,
            client_id = KeycloakContainerFixture.ClientId,
            redirect_uri = KeycloakContainerFixture.RedirectUri,
            scope = "openid email profile",
        });

        var parsed = OidcProviderConfigParser.Parse(configPayload);
        parsed.IsSuccess.Should().BeTrue(parsed.IsFailure ? parsed.Error : null);

        // Los endpoints derivados por el parser deben coincidir con los reales de Keycloak.
        parsed.Value.Endpoints.JwksUri.Should().Be(_keycloak.Endpoints.JwksUri);
        parsed.Value.Endpoints.TokenEndpoint.Should().Be(_keycloak.Endpoints.TokenEndpoint);

        // Y un id_token real debe validar contra esos endpoints derivados (JWKS real alcanzable).
        using var http = new HttpClient();
        var idToken = await RequestIdTokenViaDirectAccessGrantAsync(http, ct);

        using var jwksHttp = new HttpClient();
        var validator = new OidcIdTokenValidator(new HttpJwksProvider(jwksHttp), TimeProvider.System);
        var result = await validator.ValidateAsync(
            idToken!, parsed.Value.Endpoints, expectedAudience: KeycloakContainerFixture.ClientId,
            expectedNonce: string.Empty, ct);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Email.Should().Be(KeycloakContainerFixture.TestEmail);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pide un id_token REAL a Keycloak por Direct Access Grant (grant_type=password) con
    /// scope openid. Es un token firmado por las llaves reales del realm, aunque no sea el
    /// flujo authorization_code — suficiente para ejercer la validación real (JWKS + firma).
    /// </summary>
    private async Task<string?> RequestIdTokenViaDirectAccessGrantAsync(HttpClient http, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = KeycloakContainerFixture.ClientId,
            ["username"] = KeycloakContainerFixture.TestUsername,
            ["password"] = KeycloakContainerFixture.TestPassword,
            ["scope"] = "openid email profile",
        };

        using var response = await http.PostAsync(
            _keycloak.Endpoints.TokenEndpoint, new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        response.IsSuccessStatusCode.Should().BeTrue($"token endpoint real debe responder 200; body: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("id_token", out var idToken) ? idToken.GetString() : null;
    }

    /// <summary>
    /// Ejercita el login headless del flujo authorization_code contra Keycloak real: GET del
    /// endpoint de autorización → parsea el <c>action</c> del formulario de login → POST de
    /// credenciales → captura el 302 al redirect_uri y extrae <c>code</c>/<c>state</c>.
    /// No se sigue el redirect (redirect_uri no resuelve; solo interesa el 'code').
    /// </summary>
    private static async Task<(string Code, string State)> PerformHeadlessAuthorizationCodeLoginAsync(
        string authorizationUrl, CancellationToken ct)
    {
        // Manejo manual de cookies: la CookieContainer de .NET descarta las cookies de sesión de
        // Keycloak (KC_RESTART/AUTH_SESSION_ID con SameSite=None sobre HTTP), lo que hace que el POST
        // de login sea rechazado con 400 «cookie not found». Reenviamos los pares name=value a mano.
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
            var diagBody = await loginResponse.Content.ReadAsStringAsync(ct);
            var snippet = diagBody.Length > 600 ? diagBody[..600] : diagBody;
            throw new Xunit.Sdk.XunitException(
                $"Login POST no redirigió. Status={(int)loginResponse.StatusCode}. Cookies=[{cookieHeader}]. Action={formAction}. Body:\n{snippet}");
        }

        var location = loginResponse.Headers.Location;
        location.Should().NotBeNull("Keycloak debe devolver Location con el redirect_uri + code");

        var query = ParseQuery(location!);
        query.Should().ContainKey("code");
        query.Should().ContainKey("state");
        return (query["code"], query["state"]);
    }

    /// <summary>
    /// Extrae los pares <c>name=value</c> de todas las cabeceras <c>Set-Cookie</c> de la respuesta
    /// y los une para reenviarlos como cabecera <c>Cookie</c> en el POST de login (manejo manual,
    /// evitando las peculiaridades de <see cref="System.Net.CookieContainer"/> con Keycloak).
    /// </summary>
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
        // El template de login de Keycloak usa id="kc-form-login" con un action de POST.
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

    private static IdentityProvider BuildKeycloakProvider()
        => IdentityProvider.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create("KC"),
            Name.Create("Keycloak"),
            Description.Create("IdP corporativo (arnes real G-109)"),
            IdpStrategy.Keycloak,
            ActorId.Create("00000000-0000-0000-0000-000000000109")).Value;

    /// <summary>
    /// <see cref="IOidcProviderConfigStore"/> estático que devuelve la configuración OIDC del
    /// Keycloak del contenedor. La resolución real por prioridad/suite/dominio (FR-042) vive en
    /// otro subsistema; aquí no se prueba la resolución sino el protocolo OIDC contra el IdP real.
    /// </summary>
    private sealed class StaticOidcProviderConfigStore : IOidcProviderConfigStore
    {
        private readonly OidcProviderConfig _config;

        public StaticOidcProviderConfigStore(OidcProviderConfig config) => _config = config;

        public Task<Evolith.Ums.Domain.Kernel.Result<OidcProviderConfig>> GetAsync(
            IdentityProvider provider, CancellationToken cancellationToken = default)
            => Task.FromResult(Evolith.Ums.Domain.Kernel.Result<OidcProviderConfig>.Success(_config));
    }
}
