using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Configuration;

public sealed class ConfigurationRestTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConfigurationRestTests(UmsApiWebApplicationFactory factory)
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
    }

    [Fact]
    public async Task GetFeatureFlags_ShouldReturnSeededFlags()
    {
        var response = await _client.GetAsync("/api/v1/feature-flags?page=1&pageSize=10", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetIdpConfigurations_ShouldReturnSeededConfigurations()
    {
        var response = await _client.GetAsync("/api/v1/idp-configurations?page=1&pageSize=10", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ResolveIdpConfiguration_ShouldReturnResolvedProvider()
    {
        var response = await _client.GetAsync(
            "/api/v1/idp-configurations/resolve?tenantId=3fa85f64-5717-4562-b3fc-2c963f66afa6&systemSuiteId=11111111-1111-1111-1111-111111111111&emailDomain=beyondnet.com",
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var resolved = payload.RootElement;
        resolved.GetProperty("providerType").GetString().Should().Be("AZURE_AD");
        resolved.GetProperty("protocol").GetString().Should().Be("OIDC");
        resolved.GetProperty("domainMatched").GetBoolean().Should().BeTrue();
    }
}
