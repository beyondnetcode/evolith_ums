using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.E2E;

/// <summary>
/// E2E tests for the SystemSuite bounded context (Authorization).
/// Covers full CRUD lifecycle against a real SQL Server Testcontainer:
///   - Create / Get by ID / Get list with pagination + search
///   - Update suite metadata
///   - Status management: SetStatus (Active / Inactive)
///   - Module sub-resource: Add / Update / Deactivate / Activate / Remove
///   - AppSetting sub-resource: Add / Update / Remove
///   - Action sub-resource: Register / Remove
///   - Validation errors (400), Not-Found (404), Conflict (409)
///
/// Architecture:
///   - Commands  → REST API  (POST / PUT / DELETE)
///   - Queries   → REST API  (GET)
///
/// Each test creates its own Tenant + SystemSuite to guarantee isolation.
/// Prerequisites: Docker must be running locally.
/// </summary>
[Collection("PostgreSql")]
public sealed class SystemSuiteE2ETests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly HttpClient _client;

    public SystemSuiteE2ETests(PostgreSqlContainerFixture fixture)
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
            // ADR-0077: aprovisionar recursos (system-suites, módulos, roles, acciones, app-settings) es
            // una operación ON-BEHALF que solo el internal-admin/management-owner puede ejecutar. Estos E2E
            // fijan X-Tenant-Id a un inquilino CLIENT auxiliar (isManagementOwner:false), por lo que sin
            // contexto internal-admin explícito TenantScopePolicy devuelve AUTH_015 → 400. Se modela al
            // internal-admin de EVOLITH aprovisionando en nombre del CLIENT. Esta clase no contiene tests de
            // denegación/aislamiento, así que el header global es seguro y no altera ningún 4xx esperado.
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
    public async Task CreateSystemSuite_ValidPayload_Returns201WithId()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var response = await _client.PostAsJsonAsync("/api/v1/system-suites", NewSuitePayload(tenantId), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("systemSuiteId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateSystemSuite_MissingName_Returns400()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var payload = new { code = UniqueCode("INV"), description = "No Name" }; // Missing Name
        var res = await _client.PostAsJsonAsync("/api/v1/system-suites", payload, ct);

        // La API devuelve 400 (BadRequest) de forma consistente para errores de validación de
        // campos (p. ej. CreateUserAccount_InvalidEmail_Returns400); el 422 se reserva para
        // conflicto/duplicado. Se alinea el test al contrato real. (Convención 400 vs 422 global
        // = decisión de producto, fuera de alcance de este test.)
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSystemSuite_DuplicateCode_SameTenant_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var payload = NewSuitePayload(tenantId);
        (await _client.PostAsJsonAsync("/api/v1/system-suites", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await _client.PostAsJsonAsync("/api/v1/system-suites", payload with { Name = "Duplicate Suite" }, ct);
        dup.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // READ — via REST
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSystemSuiteById_ExistingSuite_ReturnsCorrectFields()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var payload = NewSuitePayload(tenantId);
        var createRes = await _client.PostAsJsonAsync("/api/v1/system-suites", payload, ct);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var suiteId = await ReadGuid(createRes, "systemSuiteId", ct);

        using var doc = await GetSuiteByIdAsync(suiteId, ct);
        var suite = doc.RootElement;

        suite.GetProperty("systemSuiteId").GetGuid().Should().Be(suiteId);
        suite.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        suite.GetProperty("code").GetString().Should().Be(payload.Code);
        suite.GetProperty("name").GetString().Should().Be(payload.Name);
        suite.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
        suite.GetProperty("modules").ValueKind.Should().Be(JsonValueKind.Array);
        suite.GetProperty("actions").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetSystemSuiteById_NonExistent_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var res = await _client.GetAsync($"/api/v1/system-suites/{Guid.NewGuid()}", ct);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "querying a non-existent suite ID should return 404");
    }

    [Fact]
    public async Task GetSystemSuites_Pagination_ReturnsPageMetadata()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        using var doc = await GetSuitesAsync("page=1&pageSize=5", ct);

        var list = doc.RootElement;
        list.GetProperty("page").GetInt32().Should().Be(1);
        list.GetProperty("pageSize").GetInt32().Should().Be(5);
        list.GetProperty("totalItems").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        list.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetSystemSuites_FilterByTenantId_OnlyReturnsTenantSuites()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        (await _client.PostAsJsonAsync("/api/v1/system-suites", NewSuitePayload(tenantId), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        using var doc = await GetSuitesAsync($"page=1&pageSize=50&tenantId={tenantId}", ct);

        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var item in items.EnumerateArray())
        {
            item.GetProperty("tenantId").GetGuid().Should().Be(tenantId,
                because: "tenant filter should only return suites for that tenant");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSystemSuite_ValidPayload_Returns204AndPersistsChanges()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var updatePayload = new { systemSuiteId = suiteId, name = "Updated Suite Name", description = "Updated description for E2E test" };

        var updateRes = await _client.PutAsJsonAsync($"/api/v1/system-suites/{suiteId}", updatePayload, ct);
        updateRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var doc = await GetSuiteByIdAsync(suiteId, ct);
        var suite = doc.RootElement;
        suite.GetProperty("name").GetString().Should().Be("Updated Suite Name");
        suite.GetProperty("description").GetString().Should().Be("Updated description for E2E test");
    }

    [Fact]
    public async Task UpdateSystemSuite_NonExistent_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var ghost = Guid.NewGuid();
        var payload = new { systemSuiteId = ghost, name = "Ghost", description = "Ghost suite" };

        var res = await _client.PutAsJsonAsync($"/api/v1/system-suites/{ghost}", payload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STATUS
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetSystemSuiteStatus_Inactive_Returns204AndPersistsStatus()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        // Los estados de SUITE son Active/Maintenance/Deprecated (SystemStatus); "Inactive" solo aplica a
        // módulos (ModuleStatus). Se usa "Maintenance" como estado no-activo válido.
        var payload = new { systemSuiteId = suiteId, status = "Maintenance" };
        var res = await _client.PutAsJsonAsync($"/api/v1/system-suites/{suiteId}/status", payload, ct);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var doc = await GetSuiteByIdAsync(suiteId, ct);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Maintenance");
    }

    [Fact]
    public async Task SetSystemSuiteStatus_BackToActive_Returns204()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        // El endpoint de status es PUT con body { status } (no POST con query param); estados de suite
        // válidos: Active/Maintenance/Deprecated.
        await _client.PutAsJsonAsync($"/api/v1/system-suites/{suiteId}/status", new { systemSuiteId = suiteId, status = "Maintenance" }, ct);

        var res = await _client.PutAsJsonAsync($"/api/v1/system-suites/{suiteId}/status", new { systemSuiteId = suiteId, status = "Active" }, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var doc = await GetSuiteByIdAsync(suiteId, ct);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Active");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MODULE sub-resource
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddModule_ValidPayload_Returns204AndAppearsInSuite()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var moduleCode = UniqueCode("MOD");
        var payload = new { systemSuiteId = suiteId, code = moduleCode, name = "Users Module", description = "Manages user data", sortOrder = 1 };

        var res = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules", payload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        using var doc = await GetSuiteByIdAsync(suiteId, ct);
        var modules = doc.RootElement.GetProperty("modules");
        var found = modules.EnumerateArray().Any(m => m.GetProperty("code").GetString() == moduleCode);
        found.Should().BeTrue(because: "the module should appear in the suite after being added");
    }

    [Fact]
    public async Task UpdateModule_ValidPayload_Returns204AndPersistsChanges()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var moduleCode = UniqueCode("UM");
        await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules",
            new { systemSuiteId = suiteId, code = moduleCode, name = "Original", description = "Desc", sortOrder = 1 }, ct);

        // Get moduleId via REST
        using var beforeDoc = await GetSuiteByIdAsync(suiteId, ct);
        var moduleId = beforeDoc.RootElement
            .GetProperty("modules").EnumerateArray()
            .First(m => m.GetProperty("code").GetString() == moduleCode)
            .GetProperty("id").GetGuid();

        var updatePayload = new { systemSuiteId = suiteId, moduleId, name = "Updated Module", description = "New description", sortOrder = 2 };
        var res = await _client.PutAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}", updatePayload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var afterDoc = await GetSuiteByIdAsync(suiteId, ct);
        var module = afterDoc.RootElement
            .GetProperty("modules").EnumerateArray()
            .First(m => m.GetProperty("id").GetGuid() == moduleId);
        module.GetProperty("name").GetString().Should().Be("Updated Module");
        module.GetProperty("sortOrder").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ModuleLifecycle_DeactivateActivateRemove_FullCycle()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var code = UniqueCode("LC");
        await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules",
            new { systemSuiteId = suiteId, code, name = "Lifecycle Module", description = "E2E lifecycle", sortOrder = 5 }, ct);

        // Get moduleId via REST
        using var addedDoc = await GetSuiteByIdAsync(suiteId, ct);
        var moduleId = addedDoc.RootElement
            .GetProperty("modules").EnumerateArray()
            .First(m => m.GetProperty("code").GetString() == code)
            .GetProperty("id").GetGuid();

        // Los módulos se crean Inactivos (se activan explícitamente antes de usarse); hay que activar
        // antes de poder desactivar en este ciclo de vida.
        (await _client.PostAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Deactivate
        (await _client.PostAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deactivated via REST
        using (var deactivatedDoc = await GetSuiteByIdAsync(suiteId, ct))
        {
            deactivatedDoc.RootElement
                .GetProperty("modules").EnumerateArray()
                .First(m => m.GetProperty("id").GetGuid() == moduleId)
                .GetProperty("status").GetString().Should().Be("Inactive");
        }

        // Activate
        (await _client.PostAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Deactivate again before remove (remove only works on inactive modules)
        (await _client.PostAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Remove
        (await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify gone via REST
        using var afterDoc = await GetSuiteByIdAsync(suiteId, ct);
        var still = afterDoc.RootElement
            .GetProperty("modules").EnumerateArray()
            .Any(m => m.GetProperty("id").GetGuid() == moduleId);
        still.Should().BeFalse(because: "removed module should not appear in suite");
    }

    [Fact]
    public async Task AddModule_DuplicateCode_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var code = UniqueCode("DUPM");
        var payload = new { systemSuiteId = suiteId, code, name = "Module 1", description = "Dup test", sortOrder = 1 };

        (await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules", payload, ct);
        dup.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // APP SETTING sub-resource
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAppSetting_ValidPayload_Returns204()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var payload = new { systemSuiteId = suiteId, key = $"feature.{UniqueCode("k")}", value = "true", scope = "Tenant" };

        var res = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/app-settings", payload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateAppSetting_ExistingKey_Returns204()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var key = $"setting.{UniqueCode("s")}";
        await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/app-settings",
            new { systemSuiteId = suiteId, key, value = "initial", scope = "Tenant" }, ct);

        var payload = new { systemSuiteId = suiteId, key, newValue = "updated" };
        var res = await _client.PutAsJsonAsync($"/api/v1/system-suites/{suiteId}/app-settings/{key}", payload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveAppSetting_ExistingKey_Returns204()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var key = $"del.{UniqueCode("d")}";
        await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/app-settings",
            new { systemSuiteId = suiteId, key, value = "to_delete", scope = "Global" }, ct);

        var res = await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}/app-settings/{key}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AppSettingFullCycle_Add_Update_Remove()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var key = $"full.cycle.{UniqueCode("fc")}";

        // Add
        (await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/app-settings",
            new { systemSuiteId = suiteId, key, value = "v1", scope = "Module" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        // Update
        (await _client.PutAsJsonAsync($"/api/v1/system-suites/{suiteId}/app-settings/{key}",
            new { systemSuiteId = suiteId, key, newValue = "v2" }, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Remove
        (await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}/app-settings/{key}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddAppSetting_DuplicateKey_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var key = $"dup.key.{UniqueCode("dk")}";
        var payload = new { systemSuiteId = suiteId, key, value = "v1", scope = "Tenant" };

        (await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/app-settings", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/app-settings", payload, ct);
        dup.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ACTION sub-resource
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAction_ValidPayload_Returns204AndAppearsInSuite()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var code = UniqueCode("ACT");
        var payload = new { systemSuiteId = suiteId, code, name = "Can Export Reports" };

        var res = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/actions", payload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        using var doc = await GetSuiteByIdAsync(suiteId, ct);
        var actions = doc.RootElement.GetProperty("actions");
        actions.EnumerateArray().Any(a => a.GetProperty("code").GetString() == code).Should().BeTrue(
            because: "registered action should appear in suite actions");
    }

    [Fact]
    public async Task RemoveAction_ExistingAction_Returns204AndDisappearsFromSuite()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var code = UniqueCode("DEL");
        await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/actions",
            new { systemSuiteId = suiteId, code, name = "Deletable Action" }, ct);

        var res = await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}/actions/{code}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var doc = await GetSuiteByIdAsync(suiteId, ct);
        doc.RootElement
            .GetProperty("actions").EnumerateArray()
            .Any(a => a.GetProperty("code").GetString() == code).Should().BeFalse(
                because: "removed action should not appear in suite");
    }

    [Fact]
    public async Task RegisterAction_DuplicateCode_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var code = UniqueCode("DUPACT");
        var payload = new { systemSuiteId = suiteId, code, name = "Duplicate Action" };

        (await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/actions", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/actions", payload, ct);
        dup.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveAction_NonExistentCode_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var res = await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}/actions/GHOST_CODE", ct);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gets a system suite by ID via GET /api/v1/system-suites/{id}. Caller must dispose.
    /// Fails the test if the suite does not exist (non-200).
    /// </summary>
    private async Task<JsonDocument> GetSuiteByIdAsync(Guid suiteId, CancellationToken ct)
    {
        var res = await _client.GetAsync($"/api/v1/system-suites/{suiteId}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK, because: "the suite should exist");
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
    }

    /// <summary>
    /// Gets a paged list of system suites via GET /api/v1/system-suites?{query}. Caller must dispose.
    /// The root element is the page: { items, page, pageSize, totalItems, totalPages }.
    /// </summary>
    private async Task<JsonDocument> GetSuitesAsync(string query, CancellationToken ct)
    {
        var res = await _client.GetAsync($"/api/v1/system-suites?{query}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
    }

    // Los códigos se normalizan en el dominio (DomainGuards.NormalizeCode: Trim + ToUpperInvariant +
    // espacios→'_'). El GUID :N es hex en minúsculas, así que se normaliza aquí para que el código
    // enviado coincida con el que el API almacena y devuelve (evita falsos "no aparece" por diferencia
    // de mayúsculas en las comparaciones de round-trip).
    private static string UniqueCode(string prefix)
        => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(20, prefix.Length + 32)].ToUpperInvariant();

    private record SuitePayload(Guid TenantId, string Code, string Name, string Description);
    private static SuitePayload NewSuitePayload(Guid tenantId)
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpper();
        return new SuitePayload(tenantId, $"SS{uid}", $"E2E Suite {uid}", "Created by E2E test suite");
    }

    private async Task<Guid> CreateTenantId(CancellationToken ct)
    {
        var uid = Guid.NewGuid().ToString("N")[..10].ToUpper();
        // G-037: management owner único; el inquilino CLIENT auxiliar no debe reclamarlo.
        var payload = new { code = $"T{uid}", name = $"E2E SS Tenant {uid}", type = "CLIENT", idpStrategy = (string?)null, companyReference = (string?)null, isManagementOwner = false };
        var response = await _client.PostAsJsonAsync("/api/v1/tenants", payload, ct);
        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location?.ToString();
        var idString = location!.Split('/')[^1];
        var id = Guid.Parse(idString);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", id.ToString());

        return id;
    }

    private async Task<Guid> CreateSuiteId(CancellationToken ct)
    {
        var tenantId = await CreateTenantId(ct);
        var res = await _client.PostAsJsonAsync("/api/v1/system-suites", NewSuitePayload(tenantId), ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(res, "systemSuiteId", ct);
    }

    private static async Task<Guid> ReadGuid(HttpResponseMessage response, string property, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty(property).GetGuid();
    }
}
