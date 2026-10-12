using System.Net.Http.Json;
using PactNet.Matchers;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// G-082: Consumer contract tests for the IGA API (RolePromotionRequest, ADR-UMS-093).
///
/// El grupo exige autenticación; el actor (cabecera <c>X-User-Id</c>) debe ser un GUID válido y no
/// puede coincidir con el usuario objetivo (segregación de funciones, INV-RPR3). Rutas reales:
/// <c>/api/v1/role-promotion-requests</c>. Se contrasta creación (201), violación de SoD (400),
/// lista (200), obtención por id (200) y no-encontrado por id (404). G-100: el manejador devuelve el
/// error de dominio de «no encontrado» con un código estable e idioma-agnóstico, así que el mapeador
/// lo clasifica como 404 (antes lo colapsaba a 400 por buscar sólo el substring en inglés «not found»).
/// </summary>
public sealed class IgaConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    private const string SampleGuid    = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
    private const string RequesterGuid = "0000000a-0000-0000-0000-00000000000a";
    private const string TargetGuid    = "0000000b-0000-0000-0000-00000000000b";
    private const string CurrentRole   = "0000000c-0000-0000-0000-00000000000c";
    private const string TargetRole    = "0000000d-0000-0000-0000-00000000000d";

    public IgaConsumerTests(ITestOutputHelper output)
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
    // POST /api/v1/role-promotion-requests — valid → 201
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task CreateRolePromotionRequest_WithValidData_Returns201()
    {
        _pactBuilder
            .UponReceiving("a create role promotion request with valid data")
            .WithRequest(HttpMethod.Post, "/api/v1/role-promotion-requests")
            .WithHeader("X-User-Id",    Match.Type(RequesterGuid))
            .WithHeader("Content-Type", Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithJsonBody(new
            {
                tenantId     = Match.Type(SampleGuid),
                targetUserId = Match.Type(TargetGuid),
                currentRoleId = Match.Type(CurrentRole),
                targetRoleId  = Match.Type(TargetRole),
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.Created)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                rolePromotionRequestId = Match.Regex(SampleGuid, "[0-9a-fA-F-]{36}"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", RequesterGuid);

            using var content = JsonContent.Create(new
            {
                tenantId      = SampleGuid,
                targetUserId  = TargetGuid,
                currentRoleId = CurrentRole,
                targetRoleId  = TargetRole,
            });

            var response = await client.PostAsync("/api/v1/role-promotion-requests", content);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/role-promotion-requests — SoD violation → 400
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task CreateRolePromotionRequest_ViolatingSegregation_Returns400()
    {
        _pactBuilder
            .UponReceiving("a create role promotion request that violates segregation of duties")
            .WithRequest(HttpMethod.Post, "/api/v1/role-promotion-requests")
            .WithHeader("X-User-Id",    Match.Type(RequesterGuid))
            .WithHeader("Content-Type", Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithJsonBody(new
            {
                tenantId      = Match.Type(SampleGuid),
                targetUserId  = RequesterGuid, // el objetivo coincide con el solicitante → SoD
                currentRoleId = Match.Type(CurrentRole),
                targetRoleId  = Match.Type(TargetRole),
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.BadRequest)
            .WithHeader("Content-Type", "application/problem+json")
            .WithJsonBody(new
            {
                status = Match.Type(400),
                title  = Match.Type("Bad Request"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", RequesterGuid);

            using var content = JsonContent.Create(new
            {
                tenantId      = SampleGuid,
                targetUserId  = RequesterGuid,
                currentRoleId = CurrentRole,
                targetRoleId  = TargetRole,
            });

            var response = await client.PostAsync("/api/v1/role-promotion-requests", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/role-promotion-requests
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task ListRolePromotionRequests_Returns200()
    {
        _pactBuilder
            .UponReceiving("a list request for role promotion requests")
            .Given("at least one role promotion request exists")
            .WithRequest(HttpMethod.Get, "/api/v1/role-promotion-requests")
            .WithHeader("X-User-Id", Match.Type(RequesterGuid))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(Match.MinType(new
            {
                id            = Match.Type(SampleGuid),
                tenantId      = Match.Type(SampleGuid),
                targetUserId  = Match.Type(SampleGuid),
                requesterId   = Match.Type(SampleGuid),
                currentRoleId = Match.Type(SampleGuid),
                targetRoleId  = Match.Type(SampleGuid),
                status        = Match.Type("Draft"),
            }, 1));

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", RequesterGuid);

            var response = await client.GetAsync("/api/v1/role-promotion-requests");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, json.ValueKind);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/role-promotion-requests/{id} — found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetRolePromotionRequestById_WhenFound_Returns200()
    {
        const string id = "a4a4a4a4-0000-0000-0000-000000000001";

        _pactBuilder
            .UponReceiving("a request for a specific role promotion request that exists")
            .Given($"a role promotion request with id {id} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/role-promotion-requests/{id}")
            .WithHeader("X-User-Id", Match.Type(RequesterGuid))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                id            = Match.Type(id),
                tenantId      = Match.Type(SampleGuid),
                targetUserId  = Match.Type(SampleGuid),
                requesterId   = Match.Type(SampleGuid),
                currentRoleId = Match.Type(SampleGuid),
                targetRoleId  = Match.Type(SampleGuid),
                status        = Match.Type("Draft"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", RequesterGuid);

            var response = await client.GetAsync($"/api/v1/role-promotion-requests/{id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("id", out _), "response should have 'id'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/role-promotion-requests/{id} — not found → 404 (G-100)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetRolePromotionRequestById_WhenNotFound_Returns404()
    {
        const string id = "a4a4a4a4-0000-0000-0000-0000000000ff";

        _pactBuilder
            .UponReceiving("a request for a role promotion request that does not exist")
            .Given($"no role promotion request with id {id} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/role-promotion-requests/{id}")
            .WithHeader("X-User-Id", Match.Type(RequesterGuid))
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
            client.DefaultRequestHeaders.Add("X-User-Id", RequesterGuid);

            var response = await client.GetAsync($"/api/v1/role-promotion-requests/{id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    public void Dispose() { }
}
