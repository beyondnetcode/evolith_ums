using System.Net;
using System.Text.Json;
using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

public sealed class OnboardingInboxRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OnboardingInboxRestEndpointTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        _client.DefaultRequestHeaders.Add("X-User-Id", CoreDevDataSeeder.SuperAdminUserId);
        _client.DefaultRequestHeaders.Add("X-User-Name", "Integration Tester");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", CoreDevDataSeeder.InternalAdminTenantId);
        // El super usuario sembrado del inquilino de administracion interna no tiene plantilla
        // publicada —su rol GlobalAdmin se siembra sin items—, asi que su grafo no concede nada.
        // Hasta que la tenga, atraviesa el guardia por el atajo, que aqui sigue encendido (G-280).
        _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
    }

    [Fact]
    public async Task UserSignupsInbox_ShouldExposeInternalAdminSeededPendingUser()
    {
        var response = await _client.GetAsync("/api/v1/onboarding/inbox/user-signups", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var items = payload.RootElement.EnumerateArray().ToList();

        items.Should().NotBeEmpty();
        items.Should().Contain(item =>
            string.Equals(item.GetProperty("email").GetString(), "bandeja.admin@ums.local", StringComparison.OrdinalIgnoreCase) &&
            item.GetProperty("tenantId").GetGuid() == Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId));
    }

    [Fact]
    public async Task ProfileRequestsInbox_ShouldExposeInternalAdminSeededPendingRequest()
    {
        var response = await _client.GetAsync("/api/v1/onboarding/inbox/profile-requests", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var items = payload.RootElement.EnumerateArray().ToList();

        items.Should().NotBeEmpty();
        items.Any(item =>
            item.GetProperty("justification").GetString()?.Contains("administrador interno", StringComparison.OrdinalIgnoreCase) == true)
            .Should().BeTrue();
    }
}
