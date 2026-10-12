using Evolith.Ums.Infrastructure.Persistence.Seeders;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Approvals;

public sealed class NotificationRuleRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public NotificationRuleRestEndpointTests(UmsApiWebApplicationFactory factory)
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
        _client.DefaultRequestHeaders.Add("X-User-Name", "Integration Tester");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", CoreDevDataSeeder.InternalAdminTenantId);
    }

    [Fact]
    public async Task CreateNotificationRule_WithEmailRecipient_ShouldNormalizeRecipient()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/notification-rules", new
        {
            tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            channel = "Email",
            recipient = "  Alerts@evolith.example "
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var notificationRuleId = createPayload.RootElement.GetProperty("notificationRuleId").GetGuid();

        var getResponse = await _client.GetAsync($"/api/v1/notification-rules/{notificationRuleId}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        // El normalizador de email (EmailNotificationRecipientStrategy.Normalize = Trim().ToLowerInvariant())
        // solo recorta y pasa a minúsculas; no reescribe el dominio. La entrada «  Alerts@evolith.example »
        // se canonicaliza a «alerts@evolith.example». La expectativa anterior («beyondnet.com») quedó obsoleta
        // por el rebranding beyondnet→Evolith.
        payload.RootElement.GetProperty("recipient").GetString().Should().Be("alerts@evolith.example");
        payload.RootElement.GetProperty("channel").GetString().Should().Be("Email");
    }

    [Fact]
    public async Task CreateNotificationRule_WithInvalidSmsRecipient_ShouldReturnBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/notification-rules", new
        {
            tenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId),
            channel = "Sms",
            recipient = "invalid-recipient"
        }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
