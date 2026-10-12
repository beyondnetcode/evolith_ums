using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

public sealed class UserAccountRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserAccountRestEndpointTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        _client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
        _client.DefaultRequestHeaders.Add("X-User-Name", "Integration Tester");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", CoreDevDataSeeder.InternalAdminTenantId);
        // ADR-0071 / FS-26: tras mover la propiedad de gestión al Tenant Raíz (EVOLITH), el tenant
        // sintético INTERNAL_ADMIN ya NO es management owner (isManagementOwner:false). Las escrituras
        // acotadas por EnsureManagementOwnerScopeAsync exigen contexto internal-admin explícito; sin él
        // el comando devuelve AUTH_015 → 400. Se declara el rol de operador de gestión de forma explícita.
        _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
    }

    [Fact]
    public async Task CreateUserAccount_Activate_Block_Restore_ShouldSucceed()
    {
        var email = $"integration.user.{Guid.NewGuid():N}@ums.local";
        var createResponse = await _client.PostAsJsonAsync("/api/v1/user-accounts", new
        {
            tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            branchId = (Guid?)null,
            email,
            category = "Internal",
            identityReference = "EMP-001",
            identityReferenceType = "HrId",
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var userAccountId = createdPayload.RootElement.GetProperty("userAccountId").GetGuid();

        var getCreatedResponse = await _client.GetAsync($"/api/v1/user-accounts/{userAccountId}", TestContext.Current.CancellationToken);
        getCreatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var createdUserPayload = JsonDocument.Parse(await getCreatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        createdUserPayload.RootElement.GetProperty("userAccountId").GetGuid().Should().Be(userAccountId);
        createdUserPayload.RootElement.GetProperty("status").GetString().Should().Be("Pending");
        createdUserPayload.RootElement.GetProperty("branchId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        var activateResponse = await _client.PostAsync($"/api/v1/user-accounts/{userAccountId}/activate", null, TestContext.Current.CancellationToken);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getActivatedResponse = await _client.GetAsync($"/api/v1/user-accounts/{userAccountId}", TestContext.Current.CancellationToken);
        using var activatedPayload = JsonDocument.Parse(await getActivatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        activatedPayload.RootElement.GetProperty("status").GetString().Should().Be("Active");

        var blockResponse = await _client.PostAsync($"/api/v1/user-accounts/{userAccountId}/block?reason=manual-review", null, TestContext.Current.CancellationToken);
        blockResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getBlockedResponse = await _client.GetAsync($"/api/v1/user-accounts/{userAccountId}", TestContext.Current.CancellationToken);
        using var blockedPayload = JsonDocument.Parse(await getBlockedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        blockedPayload.RootElement.GetProperty("status").GetString().Should().Be("Blocked");

        var restoreResponse = await _client.PostAsync($"/api/v1/user-accounts/{userAccountId}/restore", null, TestContext.Current.CancellationToken);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getRestoredResponse = await _client.GetAsync($"/api/v1/user-accounts/{userAccountId}", TestContext.Current.CancellationToken);
        using var restoredPayload = JsonDocument.Parse(await getRestoredResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        restoredPayload.RootElement.GetProperty("status").GetString().Should().Be("Active");

        var duplicateCreateResponse = await _client.PostAsJsonAsync("/api/v1/user-accounts", new
        {
            tenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            branchId = (Guid?)null,
            email,
            category = "Internal",
            identityReference = "EMP-002",
            identityReferenceType = "HrId",
        }, TestContext.Current.CancellationToken);

        duplicateCreateResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetUserAccountById_WhenMissing_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/user-accounts/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetLocalPassword_ForNativeUser_ShouldExposeStatusWithoutHash()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/user-accounts", new
        {
            tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            email = $"native.password.{Guid.NewGuid():N}@ums.local",
            category = "Internal",
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var userAccountId = createdPayload.RootElement.GetProperty("userAccountId").GetGuid();

        var setPasswordResponse = await _client.PostAsJsonAsync($"/api/v1/user-accounts/{userAccountId}/passwords", new
        {
            userAccountId,
            password = "Temporary!Pass123",
        }, TestContext.Current.CancellationToken);

        setPasswordResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var getResponse = await _client.GetAsync($"/api/v1/user-accounts/{userAccountId}", TestContext.Current.CancellationToken);
        using var accountPayload = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        accountPayload.RootElement.GetProperty("hasActivePassword").GetBoolean().Should().BeTrue();
        accountPayload.RootElement.GetProperty("passwordUpdatedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
        accountPayload.RootElement.TryGetProperty("passwordHash", out _).Should().BeFalse();
    }
}
