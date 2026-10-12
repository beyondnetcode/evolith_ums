using System.Net.Http.Json;
using PactNet.Matchers;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// G-082: Consumer contract tests for the Approvals API (ApprovalRequest).
///
/// Perspectiva de <b>ums-web-app</b> (consumer) → <b>ums-api</b> (provider). Sólo importa la FORMA
/// HTTP: lista paginada, obtención por id, no-encontrado (404) y el comando de creación con datos
/// inválidos (400 de validación). Rutas reales: <c>/api/v1/approval-requests</c>.
/// </summary>
public sealed class ApprovalsConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    private const string SampleGuid = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

    public ApprovalsConsumerTests(ITestOutputHelper output)
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
    // GET /api/v1/approval-requests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetApprovalRequests_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for approval requests")
            .Given("at least one approval request exists")
            .WithRequest(HttpMethod.Get, "/api/v1/approval-requests")
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
                    approvalRequestId = Match.Type(SampleGuid),
                    workflowId        = Match.Type(SampleGuid),
                    targetUserId      = Match.Type(SampleGuid),
                    status            = Match.Type("Pending"),
                    requestedSystemId = Match.Type(SampleGuid),
                    requestedRoleId   = Match.Type(SampleGuid),
                }, 1),
                totalItems = Match.Type(1),
                page       = Match.Type(1),
                pageSize   = Match.Type(10),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync("/api/v1/approval-requests?page=1&pageSize=10");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _),      "response should have 'items'");
            Assert.True(json.TryGetProperty("totalItems", out _),  "response should have 'totalItems'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/approval-requests/{id} — found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetApprovalRequestById_WhenFound_Returns200()
    {
        const string id = "a1a1a1a1-0000-0000-0000-000000000001";

        _pactBuilder
            .UponReceiving("a request for a specific approval request that exists")
            .Given($"an approval request with id {id} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/approval-requests/{id}")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                approvalRequestId = Match.Type(id),
                workflowId        = Match.Type(SampleGuid),
                targetUserId      = Match.Type(SampleGuid),
                status            = Match.Type("Pending"),
                requestedSystemId = Match.Type(SampleGuid),
                requestedRoleId   = Match.Type(SampleGuid),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync($"/api/v1/approval-requests/{id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("approvalRequestId", out _), "response should have 'approvalRequestId'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/approval-requests/{id} — not found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetApprovalRequestById_WhenNotFound_Returns404()
    {
        const string missingId = "00000000-0000-0000-0000-0000000000a1";

        _pactBuilder
            .UponReceiving("a request for an approval request that does not exist")
            .Given($"no approval request with id {missingId} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/approval-requests/{missingId}")
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

            var response = await client.GetAsync($"/api/v1/approval-requests/{missingId}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/approval-requests — invalid body → 400
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task CreateApprovalRequest_WithInvalidData_Returns400()
    {
        const string emptyGuid = "00000000-0000-0000-0000-000000000000";

        _pactBuilder
            .UponReceiving("a create approval request with invalid data")
            .WithRequest(HttpMethod.Post, "/api/v1/approval-requests")
            .WithHeader("X-User-Id",    Match.Type("dev-user"))
            .WithHeader("Content-Type", Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithJsonBody(new
            {
                workflowId        = emptyGuid,
                targetUserId      = emptyGuid,
                requestedSystemId = emptyGuid,
                requestedRoleId   = emptyGuid,
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.BadRequest)
            .WithHeader("Content-Type", "application/problem+json")
            .WithJsonBody(new
            {
                status = Match.Type(400),
                title  = Match.Type("Validation Error"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            using var content = JsonContent.Create(new
            {
                workflowId        = emptyGuid,
                targetUserId      = emptyGuid,
                requestedSystemId = emptyGuid,
                requestedRoleId   = emptyGuid,
            });

            var response = await client.PostAsync("/api/v1/approval-requests", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        });
    }

    public void Dispose() { }
}
