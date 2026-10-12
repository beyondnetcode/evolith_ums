using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// Pruebas del refresh deslizante por cookie de sesión (<c>POST /api/v1/auth/refresh</c>).
///
/// D-019 / ADR-UMS-091: el refresh por cookie debe <b>espejar el login</b> — regenerar el grafo
/// de autorización vigente y emitir un graph JWT que porte el modelo de permisos actual —, en
/// vez de re-firmar un JWT con permisos vacíos. Estas pruebas endurecen el contrato para exigir
/// que el token/respuesta refrescados lleven el conjunto de permisos vigente (no vacío) y que un
/// cambio de permisos aplicado tras el login se refleje en la renovación.
/// </summary>
public sealed class AuthenticationFlowTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly UmsApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationFlowTests(UmsApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task RefreshToken_WithValidSessionCookie_ShouldReturnNewToken()
    {
        var ct = TestContext.Current.CancellationToken;

        // 1. Iniciar sesión, capturar la cookie de sesión y los permisos del login.
        var login = await LoginAsync(ct);
        login.Permissions.Should().NotBeEmpty(
            "el usuario de prueba debe tener permisos para que la aserción de 'permisos vigentes' sea significativa");

        // 2. Refresh usando la cookie ([RequireAuthorization] valida la cookie).
        var refreshResponse = await _client.PostAsync("/api/v1/auth/refresh", null, ct);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var refreshPayload = JsonDocument.Parse(await refreshResponse.Content.ReadAsStringAsync(ct));
        var newToken = refreshPayload.RootElement.GetProperty("token").GetString();

        // 3. El token refrescado es nuevo…
        newToken.Should().NotBeNullOrEmpty();
        newToken.Should().NotBe(login.Token, "El refresh debe generar un JWT completamente nuevo.");

        // 4. …y —el fix real de D-019— porta el modelo de permisos VIGENTE, no un grafo vacío.
        var refreshPermissions = ReadPermissions(refreshPayload.RootElement);
        refreshPermissions.Should().NotBeEmpty(
            "el token refrescado debe portar el conjunto de permisos vigente, no Array.Empty");
        refreshPermissions.Should().BeEquivalentTo(login.Permissions,
            "el refresh espeja el login: el mismo grafo vigente ⇒ el mismo conjunto de permisos");

        // 5. El token en sí (no solo el cuerpo) sigue siendo un graph JWT como el del login.
        //
        // G-172 cambió QUÉ lleva ese token: los claims `perm` —uno por cada par opción-acción,
        // 16-24 KB en la cabecera de cada petición— se retiraron; el grafo viaja en el cuerpo y el
        // cliente lo cachea. Lo que la autorización del servidor lee son los `scope`, así que es
        // sobre ellos, y no sobre `perm`, sobre lo que hay que exigir contenido vigente.
        using var tokenClaims = DecodeJwtPayload(newToken!);
        tokenClaims.RootElement.TryGetProperty("scope", out var scopeClaims).Should().BeTrue(
            "el JWT refrescado debe embeber los ámbitos vigentes (graph JWT), igual que el login");
        CountClaimValues(scopeClaims).Should().BeGreaterThan(0);
        tokenClaims.RootElement.TryGetProperty("graph_valid_until", out _).Should().BeTrue(
            "el JWT refrescado debe ser un graph JWT (espejo del login), no el JWT plano heredado");
    }

    [Fact]
    public async Task RefreshToken_AfterPermissionChange_ReflectsCurrentPermissions_NotLoginSnapshot()
    {
        var ct = TestContext.Current.CancellationToken;

        // 1. Login: capturamos el conjunto de permisos vigente en ese momento.
        var login = await LoginAsync(ct);
        login.Permissions.Length.Should().BeGreaterThan(1,
            "el usuario de prueba debe tener ≥2 permisos para revocar uno y conservar el resto");

        // 2. Reconstruimos el grafo vigente EN PROCESO para localizar una opción Allow y su acción,
        //    y así saber qué permiso concreto revocar (los ids de nodo no viajan en el JSON).
        using var scope = _factory.Services.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        var profileRepo = scope.ServiceProvider.GetRequiredService<IProfileRepository>();
        var graphBuilder = scope.ServiceProvider.GetRequiredService<IAuthorizationGraphBuilder>();

        var user = await userRepo.GetByIdAsync(login.UserId, ct);
        user.Should().NotBeNull();

        var graphResult = await graphBuilder.BuildAsync(user!, login.TenantId, AuthMethod.Local(), systemCode: null, ct);
        graphResult.IsSuccess.Should().BeTrue();
        var graph = graphResult.Value;

        // G-171: la navegación dejó de ser la cadena rígida Módulo→Menú→Submenú→Opción y pasó a ser
        // un árbol de profundidad libre; el recorrido canónico es `GraphNavigation`. Esta prueba
        // seguía con los tres bucles literales y dejó de compilar con el cambio de modelo.
        var allowNode = GraphNavigation.AllNodes(graph)
            .First(n => n.Actions.Any(a => a.Effect == AccessEffect.Allow));
        var allowAction = allowNode.Actions.First(a => a.Effect == AccessEffect.Allow);
        var actionId = graph.Actions.First(a => a.Code == allowAction.ActionCode).Id;
        var revokedPermission = $"{allowNode.Code}:{allowAction.ActionCode}";
        login.Permissions.Should().Contain(revokedPermission);

        // 3. Aplicamos un CAMBIO DE PERMISOS al principal: revocamos ese permiso en su perfil activo.
        var profiles = await profileRepo.GetByUserIdAsync(login.UserId, ct);
        var profile = profiles.First(p => p.Props.TenantId.GetValue() == login.TenantId && p.IsActive);
        var permission = profile.Permissions.First(p =>
            p.IsActive && p.IsAllowed && !p.IsDenied &&
            p.TargetId.GetValue() == allowNode.Id &&
            p.ActionId.GetValue() == actionId);
        // El aggregate localiza el permiso por su Id canónico (Props.Id == GetId()), no por el Id
        // base de Entity<> (que se regenera aleatorio en construcción) — G-116.
        var deny = profile.OverridePermissionDeny(permission.GetId(), ActorId.Create("integration-test"));
        deny.IsSuccess.Should().BeTrue();
        await profileRepo.UpdateAsync(profile, ct);

        // 4. Refresh con la misma cookie.
        var refreshResponse = await _client.PostAsync("/api/v1/auth/refresh", null, ct);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var refreshPayload = JsonDocument.Parse(await refreshResponse.Content.ReadAsStringAsync(ct));
        var refreshPermissions = ReadPermissions(refreshPayload.RootElement);

        // 5. El refresh regenera el grafo: refleja los permisos ACTUALES, no la foto del login.
        refreshPermissions.Should().NotBeEmpty(
            "el token refrescado debe portar el modelo de permisos vigente, no un grafo vacío");
        refreshPermissions.Should().NotContain(revokedPermission,
            "el permiso revocado tras el login NO debe seguir presente en el token refrescado");
        refreshPermissions.Length.Should().Be(login.Permissions.Length - 1,
            "exactamente el permiso revocado debe desaparecer del conjunto vigente");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private sealed record LoginOutcome(Guid UserId, Guid TenantId, string? Token, string[] Permissions);

    private async Task<LoginOutcome> LoginAsync(CancellationToken ct)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            tenantCode = "RANSA_PERU",
            username = "gerente.operaciones@ransa.pe",
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
        var token = root.GetProperty("token").GetString();
        token.Should().NotBeNullOrEmpty();

        var userId = Guid.Parse(root.GetProperty("userId").GetString()!);
        var tenantId = Guid.Parse(root.GetProperty("tenantId").GetString()!);
        var permissions = ReadPermissions(root);

        return new LoginOutcome(userId, tenantId, token, permissions);
    }

    private static string[] ReadPermissions(JsonElement root)
    {
        if (!root.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return permissions.EnumerateArray()
            .Select(p => p.GetString() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToArray();
    }

    private static JsonDocument DecodeJwtPayload(string token)
    {
        var segments = token.Split('.');
        segments.Length.Should().BeGreaterThanOrEqualTo(2, "un JWT tiene header.payload.signature");
        var payload = segments[1];
        var padded = payload.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        var bytes = Convert.FromBase64String(padded);
        return JsonDocument.Parse(bytes);
    }

    private static int CountClaimValues(JsonElement claim)
        => claim.ValueKind == JsonValueKind.Array ? claim.GetArrayLength() : 1;
}
