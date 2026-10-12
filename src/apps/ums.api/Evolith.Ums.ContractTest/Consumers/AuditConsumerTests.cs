using System.Net.Http.Json;
using PactNet.Matchers;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// G-082: Consumer contract tests for the Audit API (AuditRecord).
///
/// La auditoría es sobre todo lectura y exige autenticación (cabecera <c>X-User-Id</c>) y
/// acotación por inquilino (parámetro <c>tenantId</c>). Rutas reales: <c>/api/v1/audit-records</c>.
/// </summary>
public sealed class AuditConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    private const string SampleGuid = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
    // Inquilino por defecto sembrado por el manejador de provider-states (RootTenantId del registro).
    private const string TenantId = "11111111-1111-1111-1111-111111111111";

    public AuditConsumerTests(ITestOutputHelper output)
    {
        var config = new PactConfig
        {
            PactDir    = PactsDir,
            Outputters = [new XunitOutput(output)],
            LogLevel   = PactLogLevel.Warn,
        };

        _pactBuilder = Pact.V4("ums-web-app", "ums-api", config).WithHttpInteractions();
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/audit-records?tenantId=…
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetAuditRecords_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for audit records scoped by tenant")
            .Given("at least one audit record exists")
            .WithRequest(HttpMethod.Get, "/api/v1/audit-records")
            .WithQuery("tenantId", Match.Equality(TenantId))
            .WithQuery("page",     Match.Equality("1"))
            .WithQuery("pageSize", Match.Equality("10"))
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                items = Match.MinType(new
                {
                    auditRecordId      = Match.Type(SampleGuid),
                    whoActed           = Match.Type(SampleGuid),
                    eventType          = Match.Type("ContractTestEvent"),
                    affectedEntityId   = Match.Type(SampleGuid),
                    affectedEntityType = Match.Type("ContractTest"),
                    rootTenantId       = Match.Type(SampleGuid),
                }, 1),
                totalItems = Match.Type(1),
                page       = Match.Type(1),
                pageSize   = Match.Type(10),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync($"/api/v1/audit-records?tenantId={TenantId}&page=1&pageSize=10");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _),     "response should have 'items'");
            Assert.True(json.TryGetProperty("totalItems", out _), "response should have 'totalItems'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/audit-records/{id} — found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetAuditRecordById_WhenFound_Returns200()
    {
        const string id = "a2a2a2a2-0000-0000-0000-000000000001";

        _pactBuilder
            .UponReceiving("a request for a specific audit record that exists")
            .Given($"an audit record with id {id} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/audit-records/{id}")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                auditRecordId      = Match.Type(id),
                whoActed           = Match.Type(SampleGuid),
                eventType          = Match.Type("ContractTestEvent"),
                affectedEntityId   = Match.Type(SampleGuid),
                affectedEntityType = Match.Type("ContractTest"),
                rootTenantId       = Match.Type(SampleGuid),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync($"/api/v1/audit-records/{id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("auditRecordId", out _), "response should have 'auditRecordId'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/audit-records/{id} — not found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetAuditRecordById_WhenNotFound_Returns404()
    {
        const string missingId = "00000000-0000-0000-0000-0000000000a2";

        _pactBuilder
            .UponReceiving("a request for an audit record that does not exist")
            .Given($"no audit record with id {missingId} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/audit-records/{missingId}")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.NotFound)
            .WithHeader("Content-Type", "application/problem+json")
            .WithJsonBody(new
            {
                status = Match.Type(404),
                title  = Match.Type("Not Found"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync($"/api/v1/audit-records/{missingId}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    public void Dispose() { }
}
