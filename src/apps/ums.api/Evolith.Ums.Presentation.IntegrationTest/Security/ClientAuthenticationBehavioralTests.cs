using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// G-042 — E2E conductual de autenticación de sistemas cliente (RoboSoft/SDK).
///
/// Verifica que DevAuthMiddleware, activo en el host de desarrollo, ya NO rompe el punto de
/// integración documentado <c>POST /api/v1/client/authenticate</c>: los inquilinos CLIENT
/// (COMEX_ANDINA, AGRONORTE) autentican por su TenantCode sin que el contexto se fije a EVOLITH.
///
/// Además ancla el endurecimiento del bypass <c>X-Disable-Dev-Auth</c>: sin credencial real
/// deja de ser un pase anónimo (fail-closed 401), pero con credencial (cookie de sesión) sigue
/// permitiendo ejercer la autenticación real.
/// </summary>
public sealed class ClientAuthenticationBehavioralTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly UmsApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ClientAuthenticationBehavioralTests(UmsApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    [Theory]
    [InlineData("COMEX_ANDINA", "usuario.impo@comexandina.com.pe")]
    [InlineData("AGRONORTE", "usuario.expo@agronorte.com.pe")]
    public async Task ClientAuthenticate_WithSeededClientTenant_ReturnsTokenAndGraph(string tenantCode, string username)
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/v1/client/authenticate", new
        {
            tenantCode,
            username,
            password = CoreDevDataSeeder.EffectiveEvolithDevPassword,
        }, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"el inquilino CLIENT {tenantCode} debe autenticar por su TenantCode y no colapsar a EVOLITH (G-042). Cuerpo: {body}");

        using var payload = JsonDocument.Parse(body);
        payload.RootElement.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
        payload.RootElement.GetProperty("tokenType").GetString().Should().Be("Bearer");
        payload.RootElement.GetProperty("graph").GetString().Should().NotBeNull();
    }

    [Fact]
    public async Task ClientAuthenticate_WithWrongPassword_ReturnsUnauthorized()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/v1/client/authenticate", new
        {
            tenantCode = "COMEX_ANDINA",
            username = "usuario.impo@comexandina.com.pe",
            password = "contraseña-incorrecta",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DisableDevAuth_WithoutCredential_OnProtectedEndpoint_IsFailClosed401()
    {
        // G-042 (endurecimiento): X-Disable-Dev-Auth:true sin credencial real ya NO permite
        // saltar la autenticación (antes → 200 anónimo). El middleware corta con 401.
        var ct = TestContext.Current.CancellationToken;

        var anonymous = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        anonymous.DefaultRequestHeaders.Add("X-Disable-Dev-Auth", "true");

        var response = await anonymous.GetAsync("/api/v1/user-accounts?page=1&pageSize=1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "sin cabecera Authorization ni cookie ums.session, el bypass debe fallar cerrado");
    }
}
