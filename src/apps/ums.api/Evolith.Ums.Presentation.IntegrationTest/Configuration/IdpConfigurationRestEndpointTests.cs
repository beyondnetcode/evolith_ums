using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Configuration;

public sealed class IdpConfigurationRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public IdpConfigurationRestEndpointTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        _client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
        // La cuenta sembrada ES administradora interna; decirlo permite resolver su grafo cuando
        // el inquilino en cabecera no es el suyo. Con el atajo apagado no concede nada por si sola.
        _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        _client.DefaultRequestHeaders.Add("X-User-Name", "Integration Tester");
    }

    [Fact]
    public async Task CreateUpdateActivateDeactivateIdpConfiguration_ShouldSucceed()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/idp-configurations", new
        {
            tenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            systemSuiteId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            providerType = "AZURE_AD",
            domainHints = new[] { "corp.local", "corp.example" },
            configPayload = "{\"authority\":\"https://login.microsoftonline.com/tenant-a\"}",
            secretRef = "kv/idp/test",
            resolutionPriority = 25,
            fallbackToId = (Guid?)null,
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var idpConfigurationId = createPayload.RootElement.GetProperty("idpConfigurationId").GetGuid();

        var getCreatedResponse = await _client.GetAsync($"/api/v1/idp-configurations/{idpConfigurationId}", TestContext.Current.CancellationToken);
        getCreatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var createdIdpPayload = JsonDocument.Parse(await getCreatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        createdIdpPayload.RootElement.GetProperty("status").GetString().Should().Be("Draft");
        createdIdpPayload.RootElement.GetProperty("version").GetInt32().Should().Be(1);

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/idp-configurations/{idpConfigurationId}", new
        {
            idpConfigurationId,
            domainHints = new[] { "corp.local", "login.corp.local" },
            configPayload = "{\"authority\":\"https://login.microsoftonline.com/tenant-b\"}",
            secretRef = "kv/idp/test-updated",
        }, TestContext.Current.CancellationToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getUpdatedResponse = await _client.GetAsync($"/api/v1/idp-configurations/{idpConfigurationId}", TestContext.Current.CancellationToken);
        using var updatedPayload = JsonDocument.Parse(await getUpdatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        updatedPayload.RootElement.GetProperty("secretRef").GetString().Should().Be("kv/idp/test-updated");
        updatedPayload.RootElement.GetProperty("version").GetInt32().Should().Be(2);

        var activateResponse = await _client.PostAsync($"/api/v1/idp-configurations/{idpConfigurationId}/activate", null, TestContext.Current.CancellationToken);
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getActivatedResponse = await _client.GetAsync($"/api/v1/idp-configurations/{idpConfigurationId}", TestContext.Current.CancellationToken);
        using var activatedPayload = JsonDocument.Parse(await getActivatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        activatedPayload.RootElement.GetProperty("status").GetString().Should().Be("Active");

        var deactivateResponse = await _client.PostAsync($"/api/v1/idp-configurations/{idpConfigurationId}/deactivate", null, TestContext.Current.CancellationToken);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getDeactivatedResponse = await _client.GetAsync($"/api/v1/idp-configurations/{idpConfigurationId}", TestContext.Current.CancellationToken);
        using var deactivatedPayload = JsonDocument.Parse(await getDeactivatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        deactivatedPayload.RootElement.GetProperty("status").GetString().Should().Be("Inactive");
    }

    [Fact]
    public async Task CreateIdpConfiguration_WithInvalidProviderType_ShouldReturnBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/idp-configurations", new
        {
            tenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            systemSuiteId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            providerType = "AzureAd",
            domainHints = new[] { "corp.local" },
            configPayload = "{\"authority\":\"https://login.microsoftonline.com/tenant-a\"}",
            secretRef = "kv/idp/test-invalid",
            resolutionPriority = 30,
            fallbackToId = (Guid?)null,
        }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetIdpConfigurationById_WhenMissing_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/idp-configurations/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResolveIdpConfiguration_WithMatchingDomain_ShouldReturnEffectiveConfiguration()
    {
        var response = await _client.GetAsync(
            "/api/v1/idp-configurations/resolve?tenantId=3fa85f64-5717-4562-b3fc-2c963f66afa6&systemSuiteId=11111111-1111-1111-1111-111111111111&emailDomain=beyondnet.com",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("providerType").GetString().Should().Be("AZURE_AD");
        payload.RootElement.GetProperty("protocol").GetString().Should().Be("OIDC");
        payload.RootElement.GetProperty("domainMatched").GetBoolean().Should().BeTrue();
        payload.RootElement.GetProperty("authority").GetString().Should().Be("https://login.microsoftonline.com/common");
    }
}
