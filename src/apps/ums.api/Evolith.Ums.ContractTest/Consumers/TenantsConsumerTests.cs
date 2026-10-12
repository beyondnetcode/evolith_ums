using System.Net.Http.Json;
using PactNet.Matchers;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// OPS-02: Consumer contract tests for the Tenants API.
///
/// These tests run from the perspective of <b>ums-web-app</b> (the React consumer).
/// They record the consumer's expectations into a Pact JSON file under /pacts/.
/// The provider verification tests then replay that file against the real API.
///
/// Rules:
///  - Use matchers (Match.Type, Match.Regex, etc.) rather than literal values so
///    the contract stays valid across data changes.
///  - Keep interaction descriptions short and in past tense ("a request for...").
///  - Never assert domain business logic here — only the HTTP shape matters.
/// </summary>
public sealed class TenantsConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    // PactNet writes the pact JSON here; provider tests read from the same path.
    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    public TenantsConsumerTests(ITestOutputHelper output)
    {
        var config = new PactConfig
        {
            PactDir    = PactsDir,
            Outputters = [new XunitOutput(output)],
            LogLevel   = PactLogLevel.Warn,
        };

        // Consumer: ums-web-app  |  Provider: ums-api
        _pactBuilder = Pact.V4("ums-web-app", "ums-api", config).WithHttpInteractions();
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/tenants
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetTenants_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for tenants")
            .Given("at least one tenant exists")
            .WithRequest(HttpMethod.Get, "/api/v1/tenants")
            .WithQuery("page",     Match.Equality("1"))
            .WithQuery("pageSize", Match.Equality("10"))
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", Match.Regex("application/json.*", "application/json; charset=utf-8"))
            .WithJsonBody(new
            {
                items      = Match.MinType(new { tenantId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"), code = Match.Type("ACME"), name = Match.Type("Acme Corp") }, 1),
                totalItems = Match.Type(1),
                page       = Match.Type(1),
                pageSize   = Match.Type(10),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync("/api/v1/tenants?page=1&pageSize=10");

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _),      "response should have 'items'");
            Assert.True(json.TryGetProperty("totalItems", out _),  "response should have 'totalItems'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/tenants/{id} — found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetTenantById_WhenFound_Returns200()
    {
        const string tenantId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

        _pactBuilder
            .UponReceiving("a request for a specific tenant that exists")
            .Given($"a tenant with id {tenantId} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/tenants/{tenantId}")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", Match.Regex("application/json.*", "application/json; charset=utf-8"))
            .WithJsonBody(new
            {
                tenantId = Match.Type(tenantId),
                code     = Match.Type("ACME"),
                name     = Match.Type("Acme Corp"),
                status   = Match.Type("Active"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync($"/api/v1/tenants/{tenantId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("tenantId", out _), "response should have 'tenantId'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/tenants/{id} — not found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetTenantById_WhenNotFound_Returns404()
    {
        const string missingId = "00000000-0000-0000-0000-000000000000";

        _pactBuilder
            .UponReceiving("a request for a tenant that does not exist")
            .Given($"no tenant with id {missingId} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/tenants/{missingId}")
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

            var response = await client.GetAsync($"/api/v1/tenants/{missingId}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/tenants
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task CreateTenant_WithValidBody_Returns201()
    {
        _pactBuilder
            .UponReceiving("a create tenant request with valid data")
            .WithRequest(HttpMethod.Post, "/api/v1/tenants")
            .WithHeader("X-User-Id",        Match.Type("dev-user"))
            .WithHeader("Content-Type",     Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithHeader("Idempotency-Key",  Match.Regex("a1b2c3d4-e5f6-7890-abcd-ef1234567890", "[0-9a-fA-F-]{36}"))
            .WithJsonBody(new
            {
                code = Match.Type("NEWCO"),
                name = Match.Type("New Company"),
                type = Match.Regex("INTERNAL", "INTERNAL|SUPPLIER|CLIENT"),
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.Created)
            .WithHeader("Content-Type", Match.Regex("application/json.*", "application/json; charset=utf-8"))
            .WithJsonBody(new
            {
                tenantId = Match.Regex(
                    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                    "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            client.DefaultRequestHeaders.Add("Idempotency-Key", "a1b2c3d4-e5f6-7890-abcd-ef1234567890"); // gitleaks:allow -- synthetic idempotency key in contract test

            using var content = JsonContent.Create(new
            {
                code = "NEWCO",
                name = "New Company",
                type = "INTERNAL",
            });

            var response = await client.PostAsync("/api/v1/tenants", content);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        });
    }

    public void Dispose() { }
}
