using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using AppConfigProvider = Evolith.Ums.Application.Configuration.Services.IConfigurationProvider;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// E2E HTTP del refresh token opaco configurable por inquilino (ADR-UMS-091 / FR-015/016, G-034).
///
/// Cierra el residual de G-034: los 12 tests unitarios de <c>RefreshAuthenticationCommandHandler</c>
/// ejercen el handler con colaboradores mockeados; aquí se recorre el <b>flujo HTTP completo</b>
/// sobre el host de integración (<see cref="UmsApiWebApplicationFactory"/>, login real con la data
/// semilla) con el flag <c>AUTH_REFRESH_TOKEN_ENABLED</c> <b>activado para un inquilino concreto</b>:
///
///   1. Fail-closed: con el flag apagado (default de ADR-UMS-088) el login <b>no</b> emite refresh token.
///   2. Activado el flag para el inquilino (config de alcance Tenant + recarga del proveedor), el
///      login <b>sí</b> emite un refresh token opaco.
///   3. Ese token <b>renueva</b> la sesión por <c>POST /api/v1/auth/refresh-token</c> (200 + rotación:
///      devuelve un refresh token nuevo, distinto del presentado).
///   4. <b>Revocación</b>: tras <c>POST /api/v1/auth/logout</c> (logout real, ADR-UMS-091/FR-016) el
///      token ya rotado/vivo <b>deja de renovar</b> (401): un token revocado no renueva.
///
/// El flag se activa por inquilino (no global): honra la cascada Global&gt;Suite&gt;Tenant&gt;Module
/// y la naturaleza multi-inquilino de la capacidad.
/// </summary>
public sealed class RefreshTokenPerTenantE2ETests : IClassFixture<UmsApiWebApplicationFactory>
{
    private const string TenantCode = "RANSA_PERU";
    private const string Username   = "gerente.operaciones@ransa.pe";

    private readonly UmsApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RefreshTokenPerTenantE2ETests(UmsApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task RefreshToken_PerTenantFlag_IssuesRenewsAndRevocationStopsRenewal()
    {
        var ct = TestContext.Current.CancellationToken;

        // 1. Login con el flag APAGADO (default): fail-closed ⇒ no se emite refresh token.
        //    De paso descubrimos el TenantId real del inquilino semilla para activarle el flag.
        var preFlag = await LoginAsync(ct);
        preFlag.RefreshToken.Should().BeNullOrEmpty(
            "con AUTH_REFRESH_TOKEN_ENABLED apagado el login no emite refresh token (fail-closed, ADR-UMS-088)");

        // 2. Activar la capacidad SOLO para ese inquilino (config de alcance Tenant) y recargar el
        //    proveedor de configuración (singleton) para que la resuelva en caliente.
        await EnableRefreshTokenForTenantAsync(preFlag.TenantId, ct);

        // 3. Login con el flag ENCENDIDO para el inquilino ⇒ ahora el login emite el refresh token opaco.
        var login = await LoginAsync(ct);
        login.RefreshToken.Should().NotBeNullOrEmpty(
            "activada la capacidad para el inquilino, el login debe emitir un refresh token opaco (FR-015)");

        // 4. El refresh token RENUEVA la sesión: 200 + rotación (nuevo refresh distinto del presentado).
        var (renewStatus, rotatedRefreshToken) = await PostRefreshTokenAsync(login.RefreshToken!, ct);
        renewStatus.Should().Be(HttpStatusCode.OK,
            "un refresh token vivo debe renovar la sesión mientras el inquilino tenga la capacidad activa");
        rotatedRefreshToken.Should().NotBeNullOrEmpty("la política rota por defecto ⇒ se emite un refresh token nuevo");
        rotatedRefreshToken.Should().NotBe(login.RefreshToken,
            "la rotación debe entregar un refresh token distinto del presentado (detección de reuso)");

        // 5. REVOCACIÓN: logout real (ADR-UMS-091/FR-016) revoca todas las familias vivas del usuario.
        //    El cliente lleva la cookie de sesión del último login, que identifica al principal.
        var logout = await _client.PostAsync("/api/v1/auth/logout", content: null, ct);
        logout.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Un token REVOCADO ya no renueva: presentar el refresh token vivo (rotado) tras el logout
        //    debe fallar (401), demostrando que la revocación es efectiva end-to-end.
        var (afterRevokeStatus, _) = await PostRefreshTokenAsync(rotatedRefreshToken!, ct);
        afterRevokeStatus.Should().Be(HttpStatusCode.Unauthorized,
            "tras la revocación, el refresh token de la familia ya no debe renovar la sesión");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private sealed record LoginOutcome(Guid UserId, Guid TenantId, string? RefreshToken);

    /// <summary>
    /// Login real contra la data semilla; captura la cookie de sesión (para el logout) y el
    /// refresh token opaco (null cuando el inquilino no activó la capacidad).
    /// </summary>
    private async Task<LoginOutcome> LoginAsync(CancellationToken ct)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = TenantCode,
            username = Username,
            password = CoreDevDataSeeder.SuperAdminPassword,
            rememberMe = false,
        }, ct);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();
        setCookieHeader.Should().NotBeNullOrEmpty();
        var cookieValue = setCookieHeader!.Split(';')[0];
        _client.DefaultRequestHeaders.Remove("Cookie");
        _client.DefaultRequestHeaders.Add("Cookie", cookieValue);

        using var payload = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync(ct));
        var root = payload.RootElement;

        var userId = Guid.Parse(root.GetProperty("userId").GetString()!);
        var tenantId = Guid.Parse(root.GetProperty("tenantId").GetString()!);
        var refreshToken = root.TryGetProperty("refreshToken", out var rt) && rt.ValueKind == JsonValueKind.String
            ? rt.GetString()
            : null;

        return new LoginOutcome(userId, tenantId, refreshToken);
    }

    private async Task<(HttpStatusCode Status, string? RefreshToken)> PostRefreshTokenAsync(
        string refreshToken, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh-token", new
        {
            refreshToken,
        }, ct);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, null);
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var newToken = payload.RootElement.TryGetProperty("refreshToken", out var rt) && rt.ValueKind == JsonValueKind.String
            ? rt.GetString()
            : null;
        return (response.StatusCode, newToken);
    }

    /// <summary>
    /// Siembra <c>AUTH_REFRESH_TOKEN_ENABLED = true</c> con alcance Tenant para el inquilino dado y
    /// recarga el proveedor de configuración (singleton) para que la política se resuelva en caliente.
    /// </summary>
    private async Task EnableRefreshTokenForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var actor = ActorId.Create("g034-e2e");

        var config = AppConfiguration.Create(
            TenantId.Load(tenantId),
            null,
            null,
            Code.Create(AppConfigurationCodes.AuthRefreshTokenEnabled),
            ConfigurationValue.Create("true"),
            Description.Create("G-034 E2E: refresh token opaco activado por inquilino."),
            isInheritable: true,
            isEncrypted: false,
            actor).Value;
        config.Publish(actor);

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<InMemoryAppConfigurationRepository>();
        repository.Seed(config);

        var provider = _factory.Services.GetRequiredService<AppConfigProvider>();
        await provider.ReloadAsync(ct);
    }
}
