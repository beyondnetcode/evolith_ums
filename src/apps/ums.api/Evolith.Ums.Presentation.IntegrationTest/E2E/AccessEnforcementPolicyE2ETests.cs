using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.E2E;

/// <summary>
/// E2E tests for the AccessEnforcementPolicy bounded context (Approvals).
/// Covers policy creation, retrieval, action updates, and lifecycle deactivation
/// against a real SQL Server Testcontainer.
///
/// Architecture:
///   - Commands → REST API
///   - Queries  → REST API
///
/// Each test creates its own Tenant + SystemSuite + Role to keep the target scope
/// isolated and traceable.
/// </summary>
[Collection("PostgreSql")]
public sealed class AccessEnforcementPolicyE2ETests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly HttpClient _client;

    public AccessEnforcementPolicyE2ETests(PostgreSqlContainerFixture fixture)
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
            // ADR-0077 (evolith_arch#18): estas pruebas aprovisionan SystemSuite + Role sobre un
            // inquilino CLIENT auxiliar (X-Tenant-Id apunta a él). Eso es una acción ON-BEHALF que
            // sólo el operador de gestión (internal-admin) puede ejecutar; sin esta cabecera,
            // TenantScopePolicy devuelve AUTH_015 → 400 en el setup. No hay tests de denegación en
            // esta clase: el único 4xx afirmado (CreatePolicy_WithoutProfileOrRole → 400) proviene
            // de la validación de dominio (falta profile/role), no del scope, y sigue siendo válido.
            _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        }
        else
        {
            _client = new HttpClient();
        }
    }

    [Fact]
    public async Task CreatePolicy_ValidRoleTarget_Returns201AndGetByIdMatches()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);
        var suiteId = await CreateSuiteId(tenantId, ct);
        var roleId = await CreateRoleId(suiteId, ct);

        var createRes = await _client.PostAsJsonAsync("/api/v1/access-enforcement-policies", new
        {
            tenantId,
            profileId = (Guid?)null,
            roleId,
            enforcementAction = "BlockUser",
        }, ct);

        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        createRes.Headers.Location.Should().NotBeNull();

        var policyId = await ReadGuid(createRes, "accessEnforcementPolicyId", ct);

        using var doc = await GetPolicyByIdAsync(policyId, ct);
        var policy = doc.RootElement;

        policy.GetProperty("accessEnforcementPolicyId").GetGuid().Should().Be(policyId);
        policy.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        policy.GetProperty("roleId").GetGuid().Should().Be(roleId);
        policy.GetProperty("profileId").ValueKind.Should().Be(JsonValueKind.Null);
        policy.GetProperty("enforcementAction").GetString().Should().Be("BlockUser");
        policy.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAction_RestrictProfile_Returns204AndPersistsChange()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var policyId = await CreatePolicyId(ct);

        var updateRes = await _client.PutAsJsonAsync($"/api/v1/access-enforcement-policies/{policyId}/action", new
        {
            policyId,
            newAction = "RestrictProfile",
        }, ct);

        updateRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var doc = await GetPolicyByIdAsync(policyId, ct);
        doc.RootElement.GetProperty("enforcementAction").GetString().Should().Be("RestrictProfile");
    }

    [Fact]
    public async Task DeactivatePolicy_Returns204AndMarksInactive()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var policyId = await CreatePolicyId(ct);

        var deactivateRes = await _client.PostAsync($"/api/v1/access-enforcement-policies/{policyId}/deactivate", null, ct);
        deactivateRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var doc = await GetPolicyByIdAsync(policyId, ct);
        doc.RootElement.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CreatePolicy_WithoutProfileOrRole_Returns400()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantId(ct);

        var response = await _client.PostAsJsonAsync("/api/v1/access-enforcement-policies", new
        {
            tenantId,
            profileId = (Guid?)null,
            roleId = (Guid?)null,
            enforcementAction = "LogOnly",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<JsonDocument> GetPolicyByIdAsync(Guid policyId, CancellationToken ct)
    {
        var response = await _client.GetAsync($"/api/v1/access-enforcement-policies/{policyId}", ct);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private async Task<Guid> CreatePolicyId(CancellationToken ct)
    {
        var tenantId = await CreateTenantId(ct);
        var suiteId = await CreateSuiteId(tenantId, ct);
        var roleId = await CreateRoleId(suiteId, ct);

        var response = await _client.PostAsJsonAsync("/api/v1/access-enforcement-policies", new
        {
            tenantId,
            profileId = (Guid?)null,
            roleId,
            enforcementAction = "BlockUser",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(response, "accessEnforcementPolicyId", ct);
    }

    private async Task<Guid> CreateRoleId(Guid systemSuiteId, CancellationToken ct)
    {
        var code = $"ROLE{Guid.NewGuid():N}"[..20];
        var response = await _client.PostAsJsonAsync($"/api/v1/system-suites/{systemSuiteId}/roles", new
        {
            systemSuiteId,
            code,
            value = $"Role {code}",
            description = "Created by AccessEnforcementPolicy E2E test",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(response, "roleId", ct);
    }

    private async Task<Guid> CreateSuiteId(Guid tenantId, CancellationToken ct)
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpper();
        var response = await _client.PostAsJsonAsync("/api/v1/system-suites", new
        {
            tenantId,
            code = $"SS{uid}",
            name = $"E2E Suite {uid}",
            description = "Created by AccessEnforcementPolicy E2E test",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(response, "systemSuiteId", ct);
    }

    private async Task<Guid> CreateTenantId(CancellationToken ct)
    {
        var uid = Guid.NewGuid().ToString("N")[..10].ToUpper();
        var response = await _client.PostAsJsonAsync("/api/v1/tenants", new
        {
            code = $"T{uid}",
            name = $"E2E Policy Tenant {uid}",
            type = "CLIENT",
            idpStrategy = (string?)null,
            companyReference = (string?)null,
            // G-037: el management owner es único a nivel de sistema; un inquilino CLIENT
            // auxiliar nunca debe reclamarlo (colisionaría con el pre-check → 409).
            isManagementOwner = false
        }, ct);
        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location?.ToString();
        var idString = location!.Split('/')[^1];
        var id = Guid.Parse(idString);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", id.ToString());

        return id;
    }

    private static async Task<Guid> ReadGuid(HttpResponseMessage response, string property, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty(property).GetGuid();
    }
}
