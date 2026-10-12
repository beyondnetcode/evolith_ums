using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using Evolith.Ums.Infrastructure.Persistence.Seeders;

namespace Evolith.Ums.Presentation.IntegrationTest.E2E;

/// <summary>
/// E2E dedicados de integridad de datos y patrón Result (gaps G-037, G-045, G-046).
///
/// Verifican que caminos que antes colapsaban a 500 ahora resuelven un
/// <c>Result.Failure</c> mapeado al estado HTTP correcto:
///   - G-037: un segundo management owner (por <c>POST /tenants</c> y por
///     <c>set-management-owner</c>) devuelve 409, no 500.
///   - G-045: un enum inválido en el payload y un listado sin paginación
///     obligatoria nunca colapsan a 500 (400/422, sin excepciones de control de flujo).
///   - G-046: una referencia <c>fallbackToId</c> colgante en una IdpConfiguration
///     se rechaza por integridad referencial (4xx), no colapsa a 500.
///
/// Usa <see cref="UmsApiWebApplicationFactory"/> (dev-seed activo → el inquilino EVOLITH
/// ya es el único management owner), de modo que las aserciones de unicidad son
/// deterministas con independencia del orden de ejecución.
/// </summary>
public sealed class IntegridadYResultPatternE2ETests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IntegridadYResultPatternE2ETests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        _client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
        // La cuenta sembrada ES administradora interna; decirlo permite resolver su grafo cuando
        // el inquilino en cabecera no es el suyo. Con el atajo apagado no concede nada por si sola.
        _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        _client.DefaultRequestHeaders.Add("X-User-Name", "integridad-e2e");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", CoreDevDataSeeder.InternalAdminTenantId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G-037 — un segundo management owner devuelve 409, no 500
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CrearSegundoManagementOwner_Devuelve409()
    {
        var ct = TestContext.Current.CancellationToken;

        // Garantiza que exista un management owner (lo crea si aún no lo hubiera).
        var primero = await _client.PostAsJsonAsync("/api/v1/tenants", NewOwnerPayload(), ct);
        primero.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);

        // Con un owner ya existente, un segundo intento debe rechazarse con 409 (no 500).
        var segundo = await _client.PostAsJsonAsync("/api/v1/tenants", NewOwnerPayload(), ct);

        segundo.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "el management owner es único a nivel de sistema; el pre-check de dominio debe resolver 409 antes de persistir");
    }

    [Fact]
    public async Task SetManagementOwner_SobreSegundoInquilino_Devuelve409()
    {
        var ct = TestContext.Current.CancellationToken;

        // Asegura que exista un management owner en el sistema.
        (await _client.PostAsJsonAsync("/api/v1/tenants", NewOwnerPayload(), ct))
            .StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);

        // Crea un inquilino CLIENT que NO es owner.
        var createRes = await _client.PostAsJsonAsync("/api/v1/tenants", NewClientPayload(), ct);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenantId = await ReadGuid(createRes, "tenantId", ct);

        // Intentar otorgarle la propiedad de gestión debe devolver 409, no 500.
        var setRes = await _client.PostAsJsonAsync(
            $"/api/v1/tenants/{tenantId}/set-management-owner", new { value = true }, ct);

        setRes.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "ya existe un management owner; el segundo debe rechazarse con 409 vía chequeo explícito de dominio");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G-045 — enum inválido y listado sin paginación no colapsan a 500
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CrearTenant_TipoEnumInvalido_NoColapsaA500()
    {
        var ct = TestContext.Current.CancellationToken;

        var payload = new
        {
            code = $"ENUM{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            name = "Tenant con tipo inválido",
            type = "NO_ES_UN_TIPO_VALIDO",
            idpStrategy = (string?)null,
            companyReference = (string?)null,
            isManagementOwner = false,
        };

        var res = await _client.PostAsJsonAsync("/api/v1/tenants", payload, ct);

        ((int)res.StatusCode).Should().BeLessThan(500,
            because: "un enum desconocido debe resolver Result.Failure (400/422), nunca una NullReferenceException que colapse a 500");
        res.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ListarSystemSuites_SinPaginacionObligatoria_NoColapsaA500()
    {
        var ct = TestContext.Current.CancellationToken;

        // page/pageSize son parámetros de query obligatorios (no anulables). Omitirlos
        // producía un 500 disfrazado de «Internal Server Error»; ahora ASP.NET lo resuelve
        // como 400 vía el GlobalExceptionHandler (BadHttpRequestException → 400).
        var res = await _client.GetAsync("/api/v1/system-suites", ct);

        ((int)res.StatusCode).Should().BeLessThan(500,
            because: "un listado sin paginación obligatoria debe devolver 400, no un 500 disfrazado");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G-046 — integridad referencial: fallbackToId colgante no colapsa a 500
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CrearIdpConfiguration_FallbackColgante_NoColapsaA500()
    {
        var ct = TestContext.Current.CancellationToken;

        var payload = new
        {
            tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            systemSuiteId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            providerType = "AZURE_AD",
            domainHints = new[] { "corp.local" },
            configPayload = "{\"authority\":\"https://login.microsoftonline.com/tenant-x\"}",
            secretRef = "kv/idp/dangling",
            resolutionPriority = 10,
            // Referencia colgante: ninguna IdpConfiguration con este id existe.
            fallbackToId = (Guid?)Guid.NewGuid(),
        };

        var res = await _client.PostAsJsonAsync("/api/v1/idp-configurations", payload, ct);

        ((int)res.StatusCode).Should().BeLessThan(500,
            because: "un fallbackToId colgante debe rechazarse por integridad referencial (4xx), nunca colapsar a 500");
        ((int)res.StatusCode).Should().BeGreaterThanOrEqualTo(400,
            because: "la creación con una referencia colgante no debe tener éxito");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static object NewOwnerPayload()
    {
        var uid = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return new
        {
            code = $"OWN{uid}",
            name = $"Owner {uid}",
            type = "INTERNAL",
            idpStrategy = (string?)null,
            companyReference = (string?)null,
            isManagementOwner = true,
        };
    }

    private static object NewClientPayload()
    {
        var uid = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return new
        {
            code = $"CLI{uid}",
            name = $"Client {uid}",
            type = "CLIENT",
            idpStrategy = (string?)null,
            companyReference = (string?)null,
            isManagementOwner = false,
        };
    }

    private static async Task<Guid> ReadGuid(HttpResponseMessage response, string property, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty(property).GetGuid();
    }
}
