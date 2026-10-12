using PactNet.Matchers;
using System.Net.Http.Json;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// OPS-02: Consumer contract tests for the User Accounts API.
///
/// Covers:
///  - Listing user accounts (paginated)
///  - Fetching a single user account
///  - Deleting (soft-delete + GDPR anonymization) — REC-16
/// </summary>
public sealed class UserAccountsConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    public UserAccountsConsumerTests(ITestOutputHelper output)
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
    // GET /api/v1/user-accounts
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task GetUserAccounts_ReturnsPagedList()
    {
        _pactBuilder
            .UponReceiving("a paginated list request for user accounts")
            .Given("at least one user account exists")
            .WithRequest(HttpMethod.Get, "/api/v1/user-accounts")
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
                    userAccountId = Match.Type("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                    email         = Match.Type("user@example.com"),
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

            var response = await client.GetAsync("/api/v1/user-accounts?page=1&pageSize=10");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("items", out _),     "response should have 'items'");
            Assert.True(json.TryGetProperty("totalItems", out _), "response should have 'totalItems'");
        });
    }

    // ─────────────────────────────────────────────────────────────
    // DELETE /api/v1/user-accounts/{id} — success
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task DeleteUserAccount_WhenFound_Returns204()
    {
        const string userId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

        _pactBuilder
            .UponReceiving("a delete request for an inactive user account")
            .Given($"a user account with id {userId} is inactive")
            .WithRequest(HttpMethod.Delete, $"/api/v1/user-accounts/{userId}")
            .WithHeader("X-User-Id", Match.Type("dev-user"))
            .WillRespond()
            .WithStatus(HttpStatusCode.NoContent);

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };
            client.DefaultRequestHeaders.Add("X-User-Id", "dev-user");

            var response = await client.DeleteAsync($"/api/v1/user-accounts/{userId}");
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // DELETE /api/v1/user-accounts/{id} — not found
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task DeleteUserAccount_WhenNotFound_Returns404()
    {
        const string missingId = "00000000-0000-0000-0000-000000000001";

        _pactBuilder
            .UponReceiving("a delete request for a user account that does not exist")
            .Given($"no user account with id {missingId} exists")
            .WithRequest(HttpMethod.Delete, $"/api/v1/user-accounts/{missingId}")
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

            var response = await client.DeleteAsync($"/api/v1/user-accounts/{missingId}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
    }

    public void Dispose() { }
}
