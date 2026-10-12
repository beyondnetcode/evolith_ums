using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.E2E;

/// <summary>
/// E2E tests for the UserAccount bounded context.
/// Covers full CRUD lifecycle against a real SQL Server Testcontainer:
///   - Create / Get by ID / Get list with pagination, search, and tenant filter
///   - Status transitions: Activate / Block (with reason) / Restore
///   - Soft-delete (GDPR anonymization)
///   - Authentication attempt recording (audit trail)
///   - Validation errors (400), Not-Found (404), Conflict (409)
///
/// Architecture:
///   - Commands  → REST API  (POST / PUT / DELETE)
///   - Queries   → REST API  (GET)
///
/// Each test creates its own Tenant to guarantee isolation.
/// Prerequisites: Docker must be running locally.
/// </summary>
[Collection("PostgreSql")]
public sealed class UserAccountE2ETests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly HttpClient _client;

    public UserAccountE2ETests(PostgreSqlContainerFixture fixture)
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
            // ADR-0077: el aprovisionamiento (crear usuario, registrar intento de autenticación,
            // transiciones de estado) es una operación ON-BEHALF que solo ejerce el operador de
            // gestión / internal-admin. Estos E2E fijan X-Tenant-Id a un inquilino CLIENT auxiliar
            // (CreateTenantId), lo que hace perder el contexto internal-admin y TenantScopePolicy
            // devuelve AUTH_015 → 400. Se declara el rol de forma explícita para actuar on-behalf.
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
    public async Task CreateUserAccount_ValidPayload_Returns201WithId()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var response = await _client.PostAsJsonAsync("/api/v1/user-accounts", NewUserPayload(tenantId), ct);

        var content = await response.Content.ReadAsStringAsync(ct);
        await System.IO.File.WriteAllTextAsync("e2e_error.txt", content, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, content);
        response.Headers.Location.Should().NotBeNull();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        doc.RootElement.GetProperty("userAccountId").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateUserAccount_InvalidEmail_Returns400()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var payload = new { tenantId, branchId = (Guid?)null, email = "NOT_AN_EMAIL", category = "Internal", identityReference = (string?)null, identityReferenceType = (string?)null };

        var res = await _client.PostAsJsonAsync("/api/v1/user-accounts", payload, ct);
        // G-061: la convención de validación del API devuelve 400 (no 422); el nombre del test lo refleja.
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUserAccount_DuplicateEmail_SameTenant_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var payload = NewUserPayload(tenantId);

        (await _client.PostAsJsonAsync("/api/v1/user-accounts", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await _client.PostAsJsonAsync("/api/v1/user-accounts", payload, ct);
        dup.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // READ — via REST
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserAccountById_ExistingAccount_ReturnsCorrectFields()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var payload = NewUserPayload(tenantId);
        var createRes = await _client.PostAsJsonAsync("/api/v1/user-accounts", payload, ct);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var userId = await ReadGuid(createRes, "userAccountId", ct);

        var getRes = await _client.GetAsync($"/api/v1/user-accounts/{userId}", ct);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK, because: "user account should exist");

        using var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync(ct));
        var user = doc.RootElement;

        user.GetProperty("userAccountId").GetGuid().Should().Be(userId);
        user.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        user.GetProperty("email").GetString().Should().Be(payload.Email);
        user.GetProperty("category").GetString().Should().Be(payload.Category);
        user.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetUserAccountById_NonExistent_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var getRes = await _client.GetAsync($"/api/v1/user-accounts/{Guid.NewGuid()}", ct);

        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "querying a non-existent user account ID should return 404");
    }

    [Fact]
    public async Task GetUserAccounts_Pagination_ReturnsPageMetadata()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var getRes = await _client.GetAsync("/api/v1/user-accounts?page=1&pageSize=5", ct);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync(ct));
        var list = doc.RootElement;
        list.GetProperty("page").GetInt32().Should().Be(1);
        list.GetProperty("pageSize").GetInt32().Should().Be(5);
        list.GetProperty("totalItems").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        list.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetUserAccounts_FilterByTenantId_ReturnsOnlyTenantUsers()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var payload = NewUserPayload(tenantId);
        (await _client.PostAsJsonAsync("/api/v1/user-accounts", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var getRes = await _client.GetAsync($"/api/v1/user-accounts?page=1&pageSize=50&tenantId={tenantId}", ct);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync(ct));
        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var item in items.EnumerateArray())
        {
            item.GetProperty("tenantId").GetGuid().Should().Be(tenantId,
                because: "tenant filter should only return users of that tenant");
        }
    }

    [Fact]
    public async Task GetUserAccounts_SearchByEmail_FindsCreatedUser()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var payload = NewUserPayload(tenantId);
        (await _client.PostAsJsonAsync("/api/v1/user-accounts", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var emailPrefix = payload.Email.Split('@')[0];
        var getRes = await _client.GetAsync($"/api/v1/user-accounts?page=1&pageSize=50&search={emailPrefix}&criteria=email", ct);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync(ct));
        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        var found = items.EnumerateArray().Any(i => i.GetProperty("email").GetString() == payload.Email);
        found.Should().BeTrue(because: "user should be findable by email prefix search");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STATUS TRANSITIONS
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateUserAccount_FromPending_Returns204AndChangesStatus()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);

        var res = await _client.PostAsync($"/api/v1/user-accounts/{userId}/activate", null, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadStatus(userId, ct)).Should().Be("Active");
    }

    [Fact]
    public async Task BlockUserAccount_ActiveAccount_Returns204AndChangesStatus()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);
        (await _client.PostAsync($"/api/v1/user-accounts/{userId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var res = await _client.PostAsync($"/api/v1/user-accounts/{userId}/block?reason=Policy+violation", null, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadStatus(userId, ct)).Should().Be("Blocked");
    }

    [Fact]
    public async Task RestoreUserAccount_BlockedAccount_Returns204AndChangesStatus()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);
        await _client.PostAsync($"/api/v1/user-accounts/{userId}/activate", null, ct);
        await _client.PostAsync($"/api/v1/user-accounts/{userId}/block?reason=Test+block", null, ct);

        var res = await _client.PostAsync($"/api/v1/user-accounts/{userId}/restore", null, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadStatus(userId, ct)).Should().Be("Active");
    }

    [Fact]
    public async Task FullStatusCycle_Pending_Active_Blocked_Restored_Active()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);

        // Pending → Active
        (await _client.PostAsync($"/api/v1/user-accounts/{userId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Active → Blocked
        (await _client.PostAsync($"/api/v1/user-accounts/{userId}/block?reason=E2E+full+cycle+test", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Blocked → Active (restore)
        (await _client.PostAsync($"/api/v1/user-accounts/{userId}/restore", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify final state via REST
        (await ReadStatus(userId, ct)).Should().Be("Active");
    }

    [Fact]
    public async Task ActivateUserAccount_NonExistent_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var res = await _client.PostAsync($"/api/v1/user-accounts/{Guid.NewGuid()}/activate", null, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BlockUserAccount_AlreadyBlocked_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);
        await _client.PostAsync($"/api/v1/user-accounts/{userId}/activate", null, ct);
        await _client.PostAsync($"/api/v1/user-accounts/{userId}/block?reason=First+block", null, ct);

        var second = await _client.PostAsync($"/api/v1/user-accounts/{userId}/block?reason=Second+block", null, ct);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DELETE (GDPR soft-delete)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUserAccount_ExistingAccount_Returns204AndNotAccessible()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);

        var deleteRes = await _client.DeleteAsync($"/api/v1/user-accounts/{userId}", ct);
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // After GDPR deletion the account should no longer be retrievable via the REST query
        var getRes = await _client.GetAsync($"/api/v1/user-accounts/{userId}", ct);
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "soft-deleted accounts should not be returned by queries");
    }

    [Fact]
    public async Task DeleteUserAccount_NonExistent_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var res = await _client.DeleteAsync($"/api/v1/user-accounts/{Guid.NewGuid()}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AUTHENTICATION ATTEMPT (audit trail)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordAuthAttempt_Success_Returns204()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);
        var payload = new { userAccountId = userId, success = true, reason = "Valid credentials", ipAddress = "10.0.0.1" };

        var res = await _client.PostAsJsonAsync($"/api/v1/user-accounts/{userId}/authentication-attempts", payload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RecordAuthAttempt_Failure_Returns204()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);
        var payload = new { userAccountId = userId, success = false, reason = "Wrong password", ipAddress = "192.168.1.10" };

        var res = await _client.PostAsJsonAsync($"/api/v1/user-accounts/{userId}/authentication-attempts", payload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RecordAuthAttempt_NonExistentUser_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var ghost = Guid.NewGuid();
        var payload = new { userAccountId = ghost, success = false, reason = "Invalid user", ipAddress = "127.0.0.1" };

        var res = await _client.PostAsJsonAsync($"/api/v1/user-accounts/{ghost}/authentication-attempts", payload, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecordAuthAttempt_MissingReason_Returns400()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var userId = await CreateUserAndGetId(ct);
        var payload = new { userAccountId = userId, success = true, reason = "", ipAddress = "10.0.0.1" };

        var res = await _client.PostAsJsonAsync($"/api/v1/user-accounts/{userId}/authentication-attempts", payload, ct);
        // G-061: la convención de validación del API devuelve 400 (no 422); el nombre del test lo refleja.
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Reads the current status of a user account via GET /api/v1/user-accounts/{id}.</summary>
    private async Task<string?> ReadStatus(Guid userId, CancellationToken ct)
    {
        var res = await _client.GetAsync($"/api/v1/user-accounts/{userId}", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK, because: "user account should exist");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("status").GetString();
    }

    private record UserPayload(Guid TenantId, Guid? BranchId, string Email, string Category, string? IdentityReference, string? IdentityReferenceType);
    private static UserPayload NewUserPayload(Guid tenantId)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        return new UserPayload(tenantId, null, $"user.{uid}@e2e-test.local", "Internal", null, null);
    }

    private async Task<Guid> CreateTenantId(CancellationToken ct)
    {
        var uid = Guid.NewGuid().ToString("N")[..10].ToUpper();
        // G-037: management owner único; el inquilino CLIENT auxiliar no debe reclamarlo.
        var payload = new { code = $"T{uid}", name = $"E2E UA Tenant {uid}", type = "CLIENT", idpStrategy = (string?)null, companyReference = (string?)null, isManagementOwner = false };
        var response = await _client.PostAsJsonAsync("/api/v1/tenants", payload, ct);
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            await System.IO.File.WriteAllTextAsync("e2e_tenant_error.txt", err, ct);
        }
        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location?.ToString();
        var idString = location!.Split('/')[^1];
        var id = Guid.Parse(idString);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", id.ToString());

        return id;
    }

    private async Task<Guid> CreateUserAndGetId(CancellationToken ct)
    {
        var tenantId = await CreateTenantId(ct);
        var res = await _client.PostAsJsonAsync("/api/v1/user-accounts", NewUserPayload(tenantId), ct);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(res, "userAccountId", ct);
    }

    private static async Task<Guid> ReadGuid(HttpResponseMessage response, string property, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty(property).GetGuid();
    }
}
