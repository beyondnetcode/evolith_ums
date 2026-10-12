using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

public sealed class IdentityAndAuthorizationRestTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IdentityAndAuthorizationRestTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task GetUserAccounts_ShouldReturnItems()
    {
        var response = await _client.GetAsync("/api/v1/user-accounts?page=1&pageSize=10", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetProfiles_ShouldReturnPayload()
    {
        var response = await _client.GetAsync("/api/v1/profiles?page=1&pageSize=10", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }
}
