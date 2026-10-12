using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Audit;

public sealed class AuditTrailCommandTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuditTrailCommandTests(UmsApiWebApplicationFactory factory)
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
        _client.DefaultRequestHeaders.Add("X-Session-Tracking-Id", "session-audit-test");
    }

    [Fact(Skip = "Audit records are dispatched via outbox which is disabled in InMemory test environment")]
    public async Task CreateFeatureFlag_ShouldEmitAuditTrailRecord()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/feature-flags", new
        {
            systemSuiteId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            tenantId = (Guid?)null,
            flagCode = $"audit_flag_{Guid.NewGuid():N}",
            flagType = "Boolean",
            flagTargets = "tenant-console",
            linkedResourceType = "Module",
            linkedResourceId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            rolloutPercentage = 100,
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var featureFlagId = createPayload.RootElement.GetProperty("featureFlagId").GetGuid();

        var found = false;
        var matchingAudit = default(JsonElement);

        for (var attempt = 0; attempt < 10 && !found; attempt++)
        {
            var auditResponse = await _client.GetAsync("/api/v1/audit-records?page=1&pageSize=20&eventType=CreateFeatureFlag", TestContext.Current.CancellationToken);
            auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            using var auditPayload = JsonDocument.Parse(await auditResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var items = auditPayload.RootElement.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                if (item.GetProperty("affectedEntityId").GetGuid() != featureFlagId
                    || item.GetProperty("eventType").GetString() != "CreateFeatureFlag")
                {
                    continue;
                }

                matchingAudit = item.Clone();
                found = true;
                break;
            }

            if (!found)
                await Task.Delay(150, TestContext.Current.CancellationToken);
        }

        found.Should().BeTrue();
        matchingAudit.GetProperty("auditResult").GetString().Should().Be("Success");
        matchingAudit.GetProperty("affectedEntityType").GetString().Should().Be("FeatureFlag");
        matchingAudit.GetProperty("whatChanged").GetString().Should().Be("CreateFeatureFlag executed");

        var metadata = matchingAudit.GetProperty("metadata").GetString();
        metadata.Should().NotBeNullOrWhiteSpace();
        metadata.Should().Contain("session-audit-test");
    }
}
