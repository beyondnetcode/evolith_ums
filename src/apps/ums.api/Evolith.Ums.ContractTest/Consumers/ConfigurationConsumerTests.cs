using PactNet.Matchers;
using System.Net.Http.Json;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// G-082: Consumer contract tests for the Configuration API (FeatureFlag + AppConfiguration).
///
/// Cubre consulta (lista + por id), un error clave (404 / 400) y comandos de creación.
/// Rutas reales: <c>/api/v1/feature-flags</c> y <c>/api/v1/app-configurations</c>.
/// </summary>
public sealed class ConfigurationConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    private const string SampleGuid = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

    public ConfigurationConsumerTests(ITestOutputHelper output)
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
    // GET /api/v1/feature-flags
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetFeatureFlags_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for feature flags")
            .Given("at least one feature flag exists")
            .WithRequest(HttpMethod.Get, "/api/v1/feature-flags")
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
                    featureFlagId = Match.Type(SampleGuid),
                    systemSuiteId = Match.Type(SampleGuid),
                    flagCode      = Match.Type("CONTRACT_TEST_FLAG"),
                    flagType      = Match.Type("Boolean"),
                    flagTargets   = Match.Type("all"),
                    status        = Match.Type("Inactive"),
                }, 1),
                totalItems = Match.Type(1),
                page       = Match.Type(1),
                pageSize   = Match.Type(10),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync("/api/v1/feature-flags?page=1&pageSize=10");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _), "response should have 'items'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/feature-flags/{id} — found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetFeatureFlagById_WhenFound_Returns200()
    {
        const string id = "a3a3a3a3-0000-0000-0000-000000000001";

        _pactBuilder
            .UponReceiving("a request for a specific feature flag that exists")
            .Given($"a feature flag with id {id} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/feature-flags/{id}")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                featureFlagId = Match.Type(id),
                systemSuiteId = Match.Type(SampleGuid),
                flagCode      = Match.Type("CONTRACT_TEST_FLAG"),
                flagType      = Match.Type("Boolean"),
                flagTargets   = Match.Type("all"),
                status        = Match.Type("Inactive"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync($"/api/v1/feature-flags/{id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("featureFlagId", out _), "response should have 'featureFlagId'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/feature-flags/{id} — not found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetFeatureFlagById_WhenNotFound_Returns404()
    {
        const string missingId = "00000000-0000-0000-0000-0000000000a3";

        _pactBuilder
            .UponReceiving("a request for a feature flag that does not exist")
            .Given($"no feature flag with id {missingId} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/feature-flags/{missingId}")
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

            var response = await client.GetAsync($"/api/v1/feature-flags/{missingId}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/feature-flags — valid → 201
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task CreateFeatureFlag_WithValidData_Returns201()
    {
        const string suiteId = "cccc3333-0000-0000-0000-000000000003";

        _pactBuilder
            .UponReceiving("a create feature flag request with valid data")
            .WithRequest(HttpMethod.Post, "/api/v1/feature-flags")
            .WithHeader("X-User-Id",    Match.Type("dev-user"))
            .WithHeader("Content-Type", Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithJsonBody(new
            {
                systemSuiteId = Match.Type(suiteId),
                flagCode      = Match.Type("PACT_CREATE_FLAG"),
                flagType      = Match.Type("Boolean"),
                flagTargets   = Match.Type("all"),
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.Created)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                featureFlagId = Match.Regex(SampleGuid, "[0-9a-fA-F-]{36}"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            using var content = JsonContent.Create(new
            {
                systemSuiteId = suiteId,
                flagCode      = "PACT_CREATE_FLAG",
                flagType      = "Boolean",
                flagTargets   = "all",
            });

            var response = await client.PostAsync("/api/v1/feature-flags", content);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/feature-flags — unsupported flag type → 400
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task CreateFeatureFlag_WithUnsupportedType_Returns400()
    {
        const string suiteId = "cccc3333-0000-0000-0000-000000000003";

        _pactBuilder
            .UponReceiving("a create feature flag request with an unsupported flag type")
            .WithRequest(HttpMethod.Post, "/api/v1/feature-flags")
            .WithHeader("X-User-Id",    Match.Type("dev-user"))
            .WithHeader("Content-Type", Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithJsonBody(new
            {
                systemSuiteId = Match.Type(suiteId),
                flagCode      = Match.Type("PACT_BAD_FLAG"),
                flagType      = "NotAFlagType",
                flagTargets   = Match.Type("all"),
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
                systemSuiteId = suiteId,
                flagCode      = "PACT_BAD_FLAG",
                flagType      = "NotAFlagType",
                flagTargets   = "all",
            });

            var response = await client.PostAsync("/api/v1/feature-flags", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // GET /api/v1/app-configurations
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetAppConfigurations_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for app configurations")
            .Given("at least one app configuration exists")
            .WithRequest(HttpMethod.Get, "/api/v1/app-configurations")
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
                    appConfigurationId = Match.Type(SampleGuid),
                    code               = Match.Type("CONTRACT_TEST_CFG"),
                    value              = Match.Type("contract-test-value"),
                    description        = Match.Type("Contract test configuration."),
                    scope              = Match.Type("Global"),
                    version            = Match.Type("1.0.0"),
                    status             = Match.Type("Draft"),
                }, 1),
                totalItems = Match.Type(1),
                page       = Match.Type(1),
                pageSize   = Match.Type(10),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync("/api/v1/app-configurations?page=1&pageSize=10");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _), "response should have 'items'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/app-configurations — valid global config → 201
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task CreateAppConfiguration_WithValidData_Returns201()
    {
        _pactBuilder
            .UponReceiving("a create app configuration request with valid global data")
            .WithRequest(HttpMethod.Post, "/api/v1/app-configurations")
            .WithHeader("X-User-Id",    Match.Type("dev-user"))
            .WithHeader("Content-Type", Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithJsonBody(new
            {
                code          = Match.Type("PACT_CREATE_CFG"),
                value         = Match.Type("pact-value"),
                description   = Match.Type("Pact created configuration."),
                isInheritable = Match.Type(true),
                isEncrypted   = Match.Type(false),
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.Created)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                appConfigurationId = Match.Regex(SampleGuid, "[0-9a-fA-F-]{36}"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            using var content = JsonContent.Create(new
            {
                code          = "PACT_CREATE_CFG",
                value         = "pact-value",
                description   = "Pact created configuration.",
                isInheritable = true,
                isEncrypted   = false,
            });

            var response = await client.PostAsync("/api/v1/app-configurations", content);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        });
    }

    public void Dispose() { }
}
