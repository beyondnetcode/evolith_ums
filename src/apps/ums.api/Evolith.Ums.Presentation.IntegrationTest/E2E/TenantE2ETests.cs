using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.E2E;

/// <summary>
/// E2E tests for the Tenant bounded context.
/// Covers full CRUD lifecycle against a real SQL Server Testcontainer:
///   - Create / Get by ID / Get list with pagination + search
///   - Status transitions: Activate / Suspend
///   - Branch sub-resource: Add / Deactivate / Reactivate / Remove
///   - Validation errors (400), Not-Found (404), Conflict (409)
///
/// Architecture:
///   - Commands  → REST API  (POST / PUT / DELETE)
///   - Queries   → REST API  (GET)
///
/// Prerequisites: Docker must be running locally.
/// Tests are automatically skipped when Docker is unavailable.
/// </summary>
[Collection("PostgreSql")]
public sealed class TenantE2ETests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly HttpClient _client;

    public TenantE2ETests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;

        if (fixture.IsAvailable)
        {
            var factory = new PostgreSqlWebApplicationFactory(fixture.ConnectionString);
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            });
            _client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
            _client.DefaultRequestHeaders.Add("X-User-Name", "e2e-test");
            // ADR-0077 (evolith_arch#18): las operaciones de Branch (Add/Deactivate/Reactivate/
            // Remove) sobre un inquilino CLIENT auxiliar (X-Tenant-Id apunta a él) son acciones
            // ON-BEHALF que sólo el operador de gestión (internal-admin) puede ejecutar; sin esta
            // cabecera, TenantScopePolicy devuelve AUTH_015 → 400 en el setup. No hay tests de
            // denegación-auth en esta clase: los 4xx afirmados (código vacío → 400, código/branch
            // duplicado → 409, inquilino/branch inexistente → 404) son validación, conflicto y
            // not-found, y siguen siendo correctos bajo internal-admin.
            _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        }
        else
        {
            _client = new HttpClient();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CREATE
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTenant_ValidPayload_Returns201WithLocation()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync("/api/v1/tenants", NewTenantPayload(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("tenantId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateTenant_MissingCode_Returns400()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var payload = new { code = "", name = "Missing Code Corp", type = "CLIENT", idpStrategy = (string?)null, companyReference = (string?)null };
        var response = await _client.PostAsJsonAsync("/api/v1/tenants", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTenant_DuplicateCode_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var payload = NewTenantPayload();
        var first = await _client.PostAsJsonAsync("/api/v1/tenants", payload, ct);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/v1/tenants", payload with { Name = "Duplicate Org" }, ct);
        second.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // READ
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTenantById_ExistingTenant_ReturnsCorrectData()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var payload = NewTenantPayload();
        var createRes = await _client.PostAsJsonAsync("/api/v1/tenants", payload, ct);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var tenantId = await ReadGuidProperty(createRes, "tenantId", ct);

        var getRes = await _client.GetAsync($"/api/v1/tenants/{tenantId}", ct);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK, because: "tenant should exist");

        using var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync(ct));
        var tenant = doc.RootElement;

        tenant.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        tenant.GetProperty("code").GetString().Should().Be(payload.Code);
        tenant.GetProperty("name").GetString().Should().Be(payload.Name);
        tenant.GetProperty("type").GetString().Should().Be(payload.Type);
        tenant.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetTenantById_NonExistent_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var res = await _client.GetAsync($"/api/v1/tenants/{Guid.NewGuid()}", ct);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "querying a non-existent tenant ID should return 404");
    }

    [Fact]
    public async Task GetTenants_Pagination_ReturnsCorrectMetadata()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var res = await _client.GetAsync("/api/v1/tenants?page=1&pageSize=5", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var list = doc.RootElement;
        list.GetProperty("page").GetInt32().Should().Be(1);
        list.GetProperty("pageSize").GetInt32().Should().Be(5);
        list.GetProperty("totalItems").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        list.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetTenants_SearchByCode_FindsTenant()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var payload = NewTenantPayload();
        (await _client.PostAsJsonAsync("/api/v1/tenants", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var res = await _client.GetAsync(
            $"/api/v1/tenants?page=1&pageSize=50&search={payload.Code}&criteria=code", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        var found = items.EnumerateArray().Any(i => i.GetProperty("code").GetString() == payload.Code);
        found.Should().BeTrue(because: "tenant should be findable by exact code search");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STATUS TRANSITIONS
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuspendAndActivateTenant_FullCycle_Returns204()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantAndGetId(ct);

        // Suspend
        var suspendRes = await _client.PostAsync($"/api/v1/tenants/{tenantId}/suspend", null, ct);
        suspendRes.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "a newly created tenant should be suspendable");

        // Verify status via REST
        (await GetTenantStatusAsync(tenantId, ct)).Should().Be("Suspended");

        // Activate
        var activateRes = await _client.PostAsync($"/api/v1/tenants/{tenantId}/activate", null, ct);
        activateRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify restored status via REST
        (await GetTenantStatusAsync(tenantId, ct)).Should().Be("Active");
    }

    [Fact]
    public async Task SuspendTenant_AlreadySuspended_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantAndGetId(ct);
        await _client.PostAsync($"/api/v1/tenants/{tenantId}/suspend", null, ct);

        var second = await _client.PostAsync($"/api/v1/tenants/{tenantId}/suspend", null, ct);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SuspendTenant_NonExistent_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var res = await _client.PostAsync($"/api/v1/tenants/{Guid.NewGuid()}/suspend", null, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ActivateTenant_NonExistent_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var res = await _client.PostAsync($"/api/v1/tenants/{Guid.NewGuid()}/activate", null, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BRANCH sub-resource
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddBranch_ValidPayload_Returns201AndAppearsInBranches()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantAndGetId(ct);
        var branchCode = $"BR{Guid.NewGuid():N}"[..8];
        var branchPayload = new { code = branchCode, name = "Main Branch", geofencingMetadata = (string?)null };

        var res = await _client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/branches", branchPayload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify via REST branches endpoint. El value object Code normaliza a MAYÚSCULAS al
        // persistir, así que la comparación debe ser insensible a mayúsculas (el código generado
        // usa hex en minúsculas de Guid:N).
        using var doc = await GetTenantBranchesAsync(tenantId, ct);
        var found = doc.RootElement.EnumerateArray()
            .Any(b => string.Equals(b.GetProperty("code").GetString(), branchCode, StringComparison.OrdinalIgnoreCase));
        found.Should().BeTrue(because: "the added branch should appear in the branches list");
    }

    [Fact]
    public async Task AddBranch_ToNonExistentTenant_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var branchPayload = new { code = "BR-GHOST", name = "Ghost Branch", geofencingMetadata = (string?)null };
        var res = await _client.PostAsJsonAsync($"/api/v1/tenants/{Guid.NewGuid()}/branches", branchPayload, ct);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BranchLifecycle_DesactivarReactivarYCerrar_CicloCompleto()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantAndGetId(ct);
        var branchCode = $"LF{Guid.NewGuid():N}"[..8];
        var branchPayload = new { code = branchCode, name = "Lifecycle Branch", geofencingMetadata = (string?)null };

        var addRes = await _client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/branches", branchPayload, ct);
        addRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Retrieve branchId via REST (AddBranchResponse only contains tenantId)
        var branchId = await GetBranchIdByCodeAsync(tenantId, branchCode, ct);

        // Desactivar y reactivar SIGUEN funcionando: ADR-0164 no toca el verbo reversible.
        (await _client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/reactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Y cerrar NO exige desactivar antes: son dos verbos independientes (ADR-0164 §2.4). Antes el
        // invariante de `RemoveBranch` obligaba a desactivar primero, porque «eliminar» era el paso
        // siguiente de «retirar del servicio»; ya no lo es.
        (await _client.DeleteAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}?reason=Cese%20de%20operaciones", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // El listado ordinario ya no la trae…
        using var after = await GetTenantBranchesAsync(tenantId, ct);
        after.RootElement.EnumerateArray().Any(b => b.GetProperty("branchId").GetGuid() == branchId)
            .Should().BeFalse(because: "una sucursal cerrada desaparece del listado operativo");

        // …pero se puede pedir, y eso es lo que distingue «oculta» de «borrada».
        var conCerradas = await _client.GetAsync($"/api/v1/tenants/{tenantId}/branches?includeClosed=true", ct);
        conCerradas.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await conCerradas.Content.ReadAsStringAsync(ct));
        var cerrada = doc.RootElement.EnumerateArray()
            .Single(b => b.GetProperty("branchId").GetGuid() == branchId);
        cerrada.GetProperty("isClosed").GetBoolean().Should().BeTrue();

        // Cerrada no se reactiva ni se desactiva: el estado terminal no tiene puerta de atrás.
        (await _client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/reactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Reintentar el cierre tampoco es idempotente en silencio: es un conflicto de estado.
        (await _client.DeleteAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Y el código NO se libera (ADR-0164 §2.3): dar de alta otra con el mismo choca de forma
        // legible, no con una violación de índice único convertida en 500.
        var reintentoDeCodigo = await _client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/branches", branchPayload, ct);
        reintentoDeCodigo.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // La bitácora conserva los cuatro episodios, con su autor y su fecha.
        var bitacora = await _client.GetAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/bitacora", ct);
        bitacora.StatusCode.Should().Be(HttpStatusCode.OK);
        using var log = JsonDocument.Parse(await bitacora.Content.ReadAsStringAsync(ct));
        var episodios = log.RootElement.EnumerateArray().Select(e => e.GetProperty("episode").GetString()).ToList();
        episodios.Should().Equal("Opened", "Deactivated", "Reactivated", "Closed");
        log.RootElement.EnumerateArray().Last().GetProperty("reason").GetString()
            .Should().Be("Cese de operaciones");
        log.RootElement.EnumerateArray()
            .All(e => !string.IsNullOrWhiteSpace(e.GetProperty("actorId").GetString()))
            .Should().BeTrue(because: "cada episodio debe decir quién lo hizo");
    }

    [Fact]
    public async Task AddBranch_DuplicateCode_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantAndGetId(ct);
        var branchCode = $"DUP{Guid.NewGuid():N}"[..8];
        var branchPayload = new { code = branchCode, name = "Dupe Branch", geofencingMetadata = (string?)null };

        (await _client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/branches", branchPayload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/branches", branchPayload, ct);
        second.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Queries GET /api/v1/tenants/{tenantId}/branches. Caller must dispose the document.</summary>
    private async Task<JsonDocument> GetTenantBranchesAsync(Guid tenantId, CancellationToken ct)
    {
        var res = await _client.GetAsync($"/api/v1/tenants/{tenantId}/branches", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK, because: "branches list should be retrievable");
        var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array,
            because: "the branches endpoint returns a JSON array");
        return doc;
    }

    /// <summary>Reads the current status of a tenant via GET /api/v1/tenants/{tenantId}.</summary>
    private async Task<string?> GetTenantStatusAsync(Guid tenantId, CancellationToken ct)
    {
        var res = await _client.GetAsync($"/api/v1/tenants/{tenantId}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("status").GetString();
    }

    /// <summary>
    /// Retrieves the branchId for a given tenant+code via the REST branches endpoint.
    /// Needed because AddBranchResponse only contains tenantId.
    /// </summary>
    private async Task<Guid> GetBranchIdByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        // El value object Code normaliza a MAYÚSCULAS al persistir; la búsqueda por código debe
        // ser insensible a mayúsculas (el código generado usa hex en minúsculas de Guid:N).
        using var doc = await GetTenantBranchesAsync(tenantId, ct);
        return doc.RootElement.EnumerateArray()
            .First(b => string.Equals(b.GetProperty("code").GetString(), code, StringComparison.OrdinalIgnoreCase))
            .GetProperty("branchId").GetGuid();
    }

    private record TenantPayload(string Code, string Name, string Type, string? IdpStrategy, string? CompanyReference, bool IsManagementOwner = false);
    private static TenantPayload NewTenantPayload()
    {
        var uid = Guid.NewGuid().ToString("N")[..10].ToUpper();
        // G-037: el management owner es único a nivel de sistema. Los inquilinos genéricos de
        // estas pruebas no deben reclamarlo — la unicidad se cubre en IntegridadYResultPatternE2ETests.
        return new TenantPayload($"T{uid}", $"E2E Tenant {uid}", "CLIENT", null, null, false);
    }

    private async Task<Guid> CreateTenantAndGetId(CancellationToken ct)
    {
        var res = await _client.PostAsJsonAsync("/api/v1/tenants", NewTenantPayload(), ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var location = res.Headers.Location?.ToString();
        var idString = location!.Split('/')[^1];
        var id = Guid.Parse(idString);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", id.ToString());

        return id;
    }

    private static async Task<Guid> ReadGuidProperty(HttpResponseMessage response, string property, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty(property).GetGuid();
    }
}
