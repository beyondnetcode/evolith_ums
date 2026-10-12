using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PactNet;
using PactNet.Matchers;
using Evolith.Ums.ContractTest.Infrastructure;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// OPS-02: Consumer contract tests for the System Suites API.
/// </summary>
public sealed class SystemSuitesConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    public SystemSuitesConsumerTests(ITestOutputHelper output)
    {
        var config = new PactConfig
        {
            PactDir    = PactsDir,
            Outputters = [new XunitOutput(output)],
            LogLevel   = PactLogLevel.Warn,
        };

        _pactBuilder = Pact.V4("ums-web-app", "ums-api", config).WithHttpInteractions();
    }

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetSystemSuites_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for system suites")
            .Given("at least one system suite exists")
            .WithRequest(HttpMethod.Get, "/api/v1/system-suites")
            .WithQuery("page",     Match.Equality("1"))
            .WithQuery("pageSize", Match.Equality("10"))
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", Match.Regex("application/json.*", "application/json; charset=utf-8"))
            .WithJsonBody(new
            {
                items = Match.MinType(new
                {
                    systemSuiteId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                    code          = Match.Type("UMS-CORE"),
                    name          = Match.Type("User Management System"),
                    status        = Match.Type("Active"),
                }, 1),
                totalItems = Match.Type(1),
                page       = Match.Type(1),
                pageSize   = Match.Type(10),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync("/api/v1/system-suites?page=1&pageSize=10");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _),     "response should have 'items'");
            Assert.True(json.TryGetProperty("totalItems", out _), "response should have 'totalItems'");
        });
    }

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetSystemSuiteById_WhenFound_Returns200()
    {
        const string suiteId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

        _pactBuilder
            .UponReceiving("a request for a specific system suite that exists")
            .Given($"a system suite with id {suiteId} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/system-suites/{suiteId}")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", Match.Regex("application/json.*", "application/json; charset=utf-8"))
            .WithJsonBody(new
            {
                systemSuiteId = Match.Type(suiteId),
                code          = Match.Type("UMS-CORE"),
                name          = Match.Type("User Management System"),
                status        = Match.Type("Active"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync($"/api/v1/system-suites/{suiteId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("systemSuiteId", out _), "response should have 'systemSuiteId'");
        });
    }

    public void Dispose() { }
}
