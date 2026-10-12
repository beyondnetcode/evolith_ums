using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

[Collection("PostgreSql")]
public sealed class PostgreSqlAuthorizationPersistenceTests : IntegrationTestBase
{
    public PostgreSqlAuthorizationPersistenceTests(PostgreSqlContainerFixture fixture) : base(fixture) { }

    // G-014 (residual): el host PostgreSQL corre con SeedDevData=false y ResetDatabase() TRUNCA toda
    // la data antes de cada test; PostgresTestSeeder no siembra NINGÚN tenant (ni RANSA ni EVOLITH).
    // Antes estas pruebas apuntaban al GUID fijo de RANSA, que ya no existe en la BD → EnsureManagementOwnerScope
    // no resuelve el tenant objetivo ("AUTH_002: Tenant not found") y el create devolvía 404. Se crea el
    // tenant objetivo por REST (como hacen los demás E2E de este host) y se opera sobre su id real.
    private async Task<Guid> CreateTenantAsync(CancellationToken ct)
    {
        var code = $"AUTHZ{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var response = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            code,
            name = $"Authz Persistence Tenant {code}",
            type = "CLIENT",
            isManagementOwner = false
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var location = response.Headers.Location?.ToString();
        return Guid.Parse(location!.Split('/')[^1]);
    }

    [Fact]
    public async Task CreateAndGetSystemSuite_UsesPostgreSqlAuthorizationStore()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker is required for SQL Server integration tests.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantAsync(ct);
        var code = $"SS{Guid.NewGuid():N}"[..10];
        var createBody = new
        {
            tenantId,
            code,
            name = "Tenant Console",
            description = "SQL-backed authorization system suite."
        };

        var createResponse = await Client.PostAsJsonAsync("/api/v1/system-suites", createBody, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(ct));
        var systemSuiteId = createPayload.RootElement.GetProperty("systemSuiteId").GetGuid();

        Client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        var getResponse = await Client.GetAsync($"/api/v1/system-suites/{systemSuiteId}", ct);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var getPayload = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(ct));
        getPayload.RootElement.GetProperty("systemSuiteId").GetGuid().Should().Be(systemSuiteId);
        getPayload.RootElement.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        // G-014 (residual): producción canonicaliza el code a MAYÚSCULAS (Code.Create →
        // DomainGuards.NormalizeCode = Trim().ToUpperInvariant()). El code enviado lleva la parte
        // del GUID en minúsculas, así que la expectativa correcta es su forma canónica en mayúsculas.
        getPayload.RootElement.GetProperty("code").GetString().Should().Be(code.ToUpperInvariant());
    }

    [Fact]
    public async Task CreatePublishAndGetPermissionTemplate_UsesPostgreSqlAuthorizationStore()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker is required for SQL Server integration tests.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await CreateTenantAsync(ct);
        var suiteCode = $"PT{Guid.NewGuid():N}"[..10];
        var createSuite = await Client.PostAsJsonAsync("/api/v1/system-suites", new
        {
            tenantId,
            code = suiteCode,
            name = "Approvals",
            description = "System suite for template integration."
        }, ct);
        createSuite.StatusCode.Should().Be(HttpStatusCode.Created);

        using var suitePayload = JsonDocument.Parse(await createSuite.Content.ReadAsStringAsync(ct));
        var systemSuiteId = suitePayload.RootElement.GetProperty("systemSuiteId").GetGuid();

        // G-014 (residual): en el host PostgreSQL las FK son reales. Antes el test usaba un roleId
        // aleatorio inexistente y el insert de la plantilla violaba FK_PermissionTemplates_Roles_RoleId
        // → DbUpdateException → 500. Se crea un rol real bajo la suite y se usa su id (FK satisfecha).
        var roleCode = $"ROLE{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        Client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        // ADR-0077 (G-014 residual): fijado X-Tenant-Id a un inquilino CLIENT, aprovisionar roles es
        // una operación ON-BEHALF que solo el operador management-owner/internal-admin puede ejecutar
        // (TenantScopePolicy → AUTH_015 → 400). Modelamos al internal-admin de EVOLITH aprovisionando
        // recursos para el CLIENT (mismo patrón que RoleE2ETests, verde 4/4).
        Client.DefaultRequestHeaders.Remove("X-Is-Internal-Admin");
        Client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        var createRole = await Client.PostAsJsonAsync($"/api/v1/system-suites/{systemSuiteId}/roles", new
        {
            code = roleCode,
            value = "Authz Persistence Role",
            description = "Rol para la prueba de round-trip del store de autorización.",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);
        createRole.StatusCode.Should().Be(HttpStatusCode.Created);
        using var rolePayload = JsonDocument.Parse(await createRole.Content.ReadAsStringAsync(ct));
        var roleId = rolePayload.RootElement.GetProperty("roleId").GetGuid();

        var createTemplate = await Client.PostAsJsonAsync("/api/v1/permission-templates", new
        {
            tenantId,
            roleId,
            systemSuiteId
        }, ct);
        createTemplate.StatusCode.Should().Be(HttpStatusCode.Created);

        using var templatePayload = JsonDocument.Parse(await createTemplate.Content.ReadAsStringAsync(ct));
        var templateId = templatePayload.RootElement.GetProperty("templateId").GetGuid();

        // G-014 (residual): publicar una plantilla exige al menos un ítem de permiso
        // (PermissionTemplate.Publish → DomainErrors.Authorization.TemplateItemsRequired). El round-trip
        // original nunca añadía ítems; con el rojo previo de AUTH_015 corregido, la publicación de una
        // plantilla vacía devolvía 400. Se añade un ítem allow válido antes de publicar. TargetId/ActionId
        // no tienen FK (solo TemplateId la tiene), así que basta con GUIDs; TargetType es un display name
        // válido de ExclusiveArcTarget ("SystemSuite").
        var addItem = await Client.PostAsJsonAsync($"/api/v1/permission-templates/{templateId}/items", new
        {
            targetType = "SystemSuite",
            targetId = systemSuiteId,
            actionId = Guid.NewGuid(),
            isAllowed = true,
            isDenied = false
        }, ct);
        addItem.StatusCode.Should().Be(HttpStatusCode.Created);

        var publishResponse = await Client.PostAsync($"/api/v1/permission-templates/{templateId}/publish", content: null, ct);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await Client.GetAsync($"/api/v1/permission-templates/{templateId}", ct);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var getPayload = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(ct));
        getPayload.RootElement.GetProperty("templateId").GetGuid().Should().Be(templateId);
        getPayload.RootElement.GetProperty("tenantId").GetGuid().Should().Be(tenantId);
        getPayload.RootElement.GetProperty("roleId").GetGuid().Should().Be(roleId);
        getPayload.RootElement.GetProperty("systemSuiteId").GetGuid().Should().Be(systemSuiteId);
        getPayload.RootElement.GetProperty("status").GetString().Should().Be("Published");

        // G-140: dar de alta una SEGUNDA plantilla para la misma terna (tenant, rol, suite) —ya
        // plantillada por la anterior— debe generar una versión nueva (0.2.0) en lugar de colisionar
        // con IX_PermissionTemplates_TenantId_RoleId_SystemSuiteId_Version → 500. La primera quedó en
        // 0.1.0; la segunda toma la siguiente.
        var createSecond = await Client.PostAsJsonAsync("/api/v1/permission-templates", new
        {
            tenantId,
            roleId,
            systemSuiteId
        }, ct);
        createSecond.StatusCode.Should().Be(HttpStatusCode.Created);

        using var secondPayload = JsonDocument.Parse(await createSecond.Content.ReadAsStringAsync(ct));
        var secondTemplateId = secondPayload.RootElement.GetProperty("templateId").GetGuid();
        secondTemplateId.Should().NotBe(templateId);

        var getSecond = await Client.GetAsync($"/api/v1/permission-templates/{secondTemplateId}", ct);
        getSecond.StatusCode.Should().Be(HttpStatusCode.OK);
        using var getSecondPayload = JsonDocument.Parse(await getSecond.Content.ReadAsStringAsync(ct));
        getSecondPayload.RootElement.GetProperty("version").GetString().Should().Be("0.2.0");
    }
}
