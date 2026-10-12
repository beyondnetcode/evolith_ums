using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Evolith.Ums.ContractTest.Infrastructure;
using PactNet;
using PactNet.Matchers;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// OPS-02: Consumer contract tests for the Permission Templates API.
/// </summary>
public sealed class PermissionTemplatesConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    public PermissionTemplatesConsumerTests(ITestOutputHelper output)
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
    public async Task GetPermissionTemplates_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for permission templates")
            .Given("at least one permission template exists")
            .WithRequest(HttpMethod.Get, "/api/v1/permission-templates")
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
                    templateId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                    tenantId = Match.Type("11111111-1111-1111-1111-111111111111"),
                    roleId = Match.Type("a5367133-fe90-46c0-ab7e-fb2a961022be"),
                    roleName = Match.Type("Administrator"),
                    systemSuiteId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                    systemSuiteName = Match.Type("User Management System"),
                    version = Match.Type("1.0.0"),
                    status = Match.Type("Published")
                }, 1),
                totalItems = Match.Type(1),
                page       = Match.Type(1),
                pageSize   = Match.Type(10),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync("/api/v1/permission-templates?page=1&pageSize=10");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _),     "response should have 'items'");
            Assert.True(json.TryGetProperty("totalItems", out _), "response should have 'totalItems'");
        });
    }

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetPermissionTemplateById_WhenFound_Returns200()
    {
        const string templateId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

        _pactBuilder
            .UponReceiving("a request for a specific permission template that exists")
            .Given($"a permission template with id {templateId} exists")
            .WithRequest(HttpMethod.Get, $"/api/v1/permission-templates/{templateId}")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", Match.Regex("application/json.*", "application/json; charset=utf-8"))
            .WithJsonBody(new
            {
                templateId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                tenantId = Match.Type("11111111-1111-1111-1111-111111111111"),
                roleId = Match.Type("a5367133-fe90-46c0-ab7e-fb2a961022be"),
                roleName = Match.Type("Administrator"),
                systemSuiteId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                systemSuiteName = Match.Type("User Management System"),
                version = Match.Type("1.0.0"),
                status = Match.Type("Published"),
                items = new object[0]
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync($"/api/v1/permission-templates/{templateId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("templateId", out _), "response should have 'templateId'");
        });
    }

    public void Dispose() { }
}
