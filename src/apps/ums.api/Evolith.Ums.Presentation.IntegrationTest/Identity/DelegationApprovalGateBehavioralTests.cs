using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

/// <summary>
/// G-056 — E2E dedicado de la compuerta de aprobación de <c>UserManagementDelegation</c>.
///
/// Prueba, extremo a extremo por REST, que una delegación creada con <c>requiresApproval=true</c>
/// NO puede activarse por la vía directa <c>POST /delegations/{id}/activate</c> (fail-closed): la
/// única promoción legítima a Active es SubmitForApproval → Approve. Como control positivo, una
/// delegación sin aprobación requerida sí se activa por esa misma vía.
///
/// Los dos administradores son usuarios internos EVOLITH sembrados (Callao, índices 1 y 2). Sus
/// GUID se derivan del GUID del inquilino EVOLITH igual que en el seeder (byte[0] = índice).
/// </summary>
public sealed class DelegationApprovalGateBehavioralTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly UmsApiWebApplicationFactory _factory;

    public DelegationApprovalGateBehavioralTests(UmsApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static Guid DeriveEvolithUserId(byte index)
    {
        var bytes = Guid.Parse(CoreDevDataSeeder.EvolithTenantId).ToByteArray();
        bytes[0] = index;
        return new Guid(bytes);
    }

    private HttpClient CreateClientAs(Guid delegatingAdminId)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-User-Id", delegatingAdminId.ToString());
        client.DefaultRequestHeaders.Add("X-User-Name", "Delegating Admin");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", CoreDevDataSeeder.EvolithTenantId);
        return client;
    }

    private static object BuildCreateBody(Guid delegatingAdminId, Guid delegatedAdminId, bool requiresApproval) => new
    {
        tenantId = Guid.Parse(CoreDevDataSeeder.EvolithTenantId),
        delegatingAdminId,
        delegatedAdminId,
        scopeType = "Tenant",
        scopeId = (Guid?)null,
        allowedActions = new[] { "CreateUser" },
        validFrom = DateTimeOffset.UtcNow,
        validUntil = DateTimeOffset.UtcNow.AddDays(15),
        maxDurationDays = (int?)null,
        requiresApproval,
    };

    [Fact]
    public async Task Activate_WhenRequiresApproval_IsRejected_FailClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        var delegatingAdminId = DeriveEvolithUserId(1); // admin.callao@evolith.example
        var delegatedAdminId = DeriveEvolithUserId(2);  // agente.aduanas.callao@evolith.example
        var client = CreateClientAs(delegatingAdminId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/delegations", BuildCreateBody(delegatingAdminId, delegatedAdminId, requiresApproval: true), ct);

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, because: createBody);

        using var created = JsonDocument.Parse(createBody);
        var delegationId = created.RootElement.GetProperty("delegationId").GetGuid();

        var activateResponse = await client.PostAsync($"/api/v1/delegations/{delegationId}/activate", null, ct);

        activateResponse.StatusCode.Should().NotBe(HttpStatusCode.NoContent,
            because: "una delegación que exige aprobación no puede activarse por la vía directa (G-056)");
        activateResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Activate_WhenNoApprovalRequired_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var delegatingAdminId = DeriveEvolithUserId(1);
        var delegatedAdminId = DeriveEvolithUserId(2);
        var client = CreateClientAs(delegatingAdminId);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/delegations", BuildCreateBody(delegatingAdminId, delegatedAdminId, requiresApproval: false), ct);

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, because: createBody);

        using var created = JsonDocument.Parse(createBody);
        var delegationId = created.RootElement.GetProperty("delegationId").GetGuid();

        var activateResponse = await client.PostAsync($"/api/v1/delegations/{delegationId}/activate", null, ct);

        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "sin aprobación requerida, la activación directa es la vía legítima");
    }
}
