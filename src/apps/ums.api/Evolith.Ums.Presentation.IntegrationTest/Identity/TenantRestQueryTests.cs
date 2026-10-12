using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

public sealed class TenantRestQueryTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TenantRestQueryTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task GetTenants_ShouldReturnSeededTenants()
    {
        var response = await _client.GetAsync("/api/v1/tenants?page=1&pageSize=10", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        payload.RootElement.GetProperty("totalItems").GetInt32().Should().BeGreaterThan(0);

        var firstTenant = payload.RootElement.GetProperty("items")[0];
        firstTenant.GetProperty("tenantId").GetGuid().Should().NotBeEmpty();
        firstTenant.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
        firstTenant.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
