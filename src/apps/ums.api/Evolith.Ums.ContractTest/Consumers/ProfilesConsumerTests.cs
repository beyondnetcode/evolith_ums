using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PactNet;
using PactNet.Matchers;
using Evolith.Ums.ContractTest.Infrastructure;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// OPS-02: Consumer contract tests for the Profiles API.
/// </summary>
public sealed class ProfilesConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    public ProfilesConsumerTests(ITestOutputHelper output)
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
    public async Task GetProfiles_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for profiles")
            .Given("at least one profile exists")
            .WithRequest(HttpMethod.Get, "/api/v1/profiles")
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
                    profileId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                    tenantId = Match.Type("11111111-1111-1111-1111-111111111111"),
                    tenantCode = Match.Type("ACME"),
                    tenantName = Match.Type("Acme Corp"),
                    userId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                    userEmail = Match.Type("user@example.com"),
                    roleId = Match.Type("a5367133-fe90-46c0-ab7e-fb2a961022be"),
                    roleCode = Match.Type("ADMIN"),
                    roleName = Match.Type("Administrator"),
                    systemSuiteId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                    systemSuiteCode = Match.Type("UMS-CORE"),
                    systemSuiteName = Match.Type("User Management System"),
                    scope = Match.Type("OrgWide"),
                    isActive = Match.Type(true),
                    permissionCount = Match.Type(0),
                    permissions = new object[0]
                }, 1),
                totalItems = Match.Type(1),
                page       = Match.Type(1),
                pageSize   = Match.Type(10),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.GetAsync("/api/v1/profiles?page=1&pageSize=10");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _),     "response should have 'items'");
            Assert.True(json.TryGetProperty("totalItems", out _), "response should have 'totalItems'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/profiles/{id}/deactivate
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task DeactivateProfile_WhenFound_Returns200()
    {
        const string profileId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

        _pactBuilder
            .UponReceiving("a deactivate request for an active profile")
            .Given($"a profile with id {profileId} exists")
            .WithRequest(HttpMethod.Post, $"/api/v1/profiles/{profileId}/deactivate")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.NoContent);

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.PostAsync($"/api/v1/profiles/{profileId}/deactivate", null);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/profiles/{id}/deactivate — not found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task DeactivateProfile_WhenNotFound_Returns404()
    {
        const string missingId = "00000000-0000-0000-0000-000000000001";

        _pactBuilder
            .UponReceiving("a deactivate request for a profile that does not exist")
            .Given($"no profile with id {missingId} exists")
            .WithRequest(HttpMethod.Post, $"/api/v1/profiles/{missingId}/deactivate")
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

            var response = await client.PostAsync($"/api/v1/profiles/{missingId}/deactivate", null);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    public void Dispose() { }
}
