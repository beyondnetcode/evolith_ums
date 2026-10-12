using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.E2E;

/// <summary>
/// E2E tests for the Role bounded context (Authorization).
/// Covers creation, update, lifecycle status, and REST list exposure against
/// a real SQL Server Testcontainer.
///
/// Each test creates its own Tenant + SystemSuite to guarantee isolation.
/// </summary>
[Collection("PostgreSql")]
public sealed class RoleE2ETests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly HttpClient _client;

    public RoleE2ETests(PostgreSqlContainerFixture fixture)
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
            // ADR-0077 (G-014 residual): tras fijar X-Tenant-Id a un inquilino CLIENT auxiliar,
            // el aprovisionamiento (suites/roles) es una operación ON-BEHALF que solo el
            // operador management-owner/internal-admin puede ejecutar (TenantScopePolicy → AUTH_015).
            // Estos E2E aprovisionan recursos para el CLIENT, modelando al internal-admin de EVOLITH.
            _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        }
        else
        {
            _client = new HttpClient();
        }
    }

    [Fact]
    public async Task CreateRole_ValidPayload_Returns201AndAppearsInRestList()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var code = UniqueCode("ROLE");

        var createResponse = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/roles", new
        {
            systemSuiteId = suiteId,
            code,
            value = "Security Administrator",
            description = "Maintains access roles",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var roleId = await ReadGuid(createResponse, "roleId", ct);

        using var doc = await GetRolesBySystemSuiteAsync(suiteId, ct);

        doc.RootElement.EnumerateArray().Any(role => role.GetProperty("roleId").GetGuid() == roleId).Should().BeTrue(
            because: "the created role should be visible in the system suite roles list");
    }

    [Fact]
    public async Task UpdateRole_ValidPayload_Returns204AndPersistsChanges()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var roleId = await CreateRoleId(suiteId, ct);

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/system-suites/{suiteId}/roles/{roleId}", new
        {
            roleId,
            value = "Security Lead",
            description = "Updated description",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 1,
        }, ct);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var doc = await GetRolesBySystemSuiteAsync(suiteId, ct);
        var role = doc.RootElement
            .EnumerateArray()
            .First(candidate => candidate.GetProperty("roleId").GetGuid() == roleId);

        role.GetProperty("value").GetString().Should().Be("Security Lead");
        role.GetProperty("description").GetString().Should().Be("Updated description");
        role.GetProperty("promotionOrder").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task SetRoleStatus_DeactivateAndReactivate_Returns204AndPersistsStatus()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var roleId = await CreateRoleId(suiteId, ct);

        var deactivateResponse = await _client.PostAsync($"/api/v1/system-suites/{suiteId}/roles/{roleId}/deactivate", null, ct);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var afterDeactivate = await GetRolesBySystemSuiteAsync(suiteId, ct))
        {
            afterDeactivate.RootElement
                .EnumerateArray()
                .First(candidate => candidate.GetProperty("roleId").GetGuid() == roleId)
                .GetProperty("isActive").GetBoolean().Should().BeFalse();
        }

        var activateResponse = await _client.PostAsync($"/api/v1/system-suites/{suiteId}/roles/{roleId}/activate", null, ct);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var afterActivate = await GetRolesBySystemSuiteAsync(suiteId, ct);
        afterActivate.RootElement
            .EnumerateArray()
            .First(candidate => candidate.GetProperty("roleId").GetGuid() == roleId)
            .GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task CreateRole_DuplicateCode_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var code = UniqueCode("DUPROLE");

        var payload = new
        {
            systemSuiteId = suiteId,
            code,
            value = "Role 1",
            description = "Dup test",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        };

        (await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/roles", payload, ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/roles", payload, ct);
        duplicate.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    private async Task<JsonDocument> GetRolesBySystemSuiteAsync(Guid suiteId, CancellationToken ct)
    {
        var response = await _client.GetAsync($"/api/v1/system-suites/{suiteId}/roles", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    private async Task<Guid> CreateRoleId(Guid suiteId, CancellationToken ct)
    {
        var code = UniqueCode("ROLE");
        var response = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/roles", new
        {
            systemSuiteId = suiteId,
            code,
            value = $"Role {code}",
            description = "Created by Role E2E test",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(response, "roleId", ct);
    }

    private async Task<Guid> CreateSuiteId(CancellationToken ct)
    {
        var tenantId = await CreateTenantId(ct);
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpper();
        var response = await _client.PostAsJsonAsync("/api/v1/system-suites", new
        {
            tenantId,
            code = $"SS{uid}",
            name = $"E2E Suite {uid}",
            description = "Created by Role E2E test",
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
            name = $"E2E Role Tenant {uid}",
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

    private static string UniqueCode(string prefix)
        => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(20, prefix.Length + 32)];

    private static async Task<Guid> ReadGuid(HttpResponseMessage response, string property, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty(property).GetGuid();
    }
}
