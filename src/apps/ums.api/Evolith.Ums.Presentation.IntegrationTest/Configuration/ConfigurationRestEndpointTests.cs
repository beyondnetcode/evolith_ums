using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Configuration;

public sealed class ConfigurationRestEndpointTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConfigurationRestEndpointTests(UmsApiWebApplicationFactory factory)
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
    public async Task GetFeatureFlags_ShouldReturnSeededFeatureFlag()
    {
        var response = await _client.GetAsync("/api/v1/feature-flags?page=1&pageSize=10", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        payload.RootElement.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        payload.RootElement.GetProperty("items")[0].GetProperty("flagCode").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateFeatureFlag_ThenGetById_ShouldSucceed()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/feature-flags", new
        {
            systemSuiteId = "11111111-1111-1111-1111-111111111111",
            tenantId = (string?)null,
            flagCode = $"tenant_feature_{Guid.NewGuid():N}",
            flagType = "Boolean",
            flagTargets = "tenant-console",
            linkedResourceType = "Module",
            linkedResourceId = "33333333-3333-3333-3333-333333333333",
            rolloutPercentage = (int?)null,
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var featureFlagId = createdPayload.RootElement.GetProperty("featureFlagId").GetGuid();

        var getResponse = await _client.GetAsync($"/api/v1/feature-flags/{featureFlagId}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var getPayload = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        getPayload.RootElement.GetProperty("featureFlagId").GetGuid().Should().Be(featureFlagId);
    }

    [Fact]
    public async Task GetAppConfigurations_ShouldReturnSeededConfiguration()
    {
        var response = await _client.GetAsync("/api/v1/app-configurations?page=1&pageSize=100", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var items = payload.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        // Verify the well-known seeded config exists (order-independent)
        var codes = Enumerable.Range(0, items.GetArrayLength())
            .Select(i => items[i].GetProperty("code").GetString())
            .ToList();
        codes.Should().Contain("SESSION_TIMEOUT_MINUTES");
    }

    /// <summary>
    /// G-104: eliminar un criterio inexistente de un feature flag EXISTENTE debe resolver 404.
    /// El flag existe (se crea antes), así que el handler pasa la guarda de «flag no encontrado» y
    /// el dominio devuelve el código estable <c>configuration.criteria_not_found</c> al direccionar el
    /// criterio por su id. Antes ese código ni estaba en la lista del <c>DomainErrorStatusMapper</c> ni
    /// contenía la frase inglesa «not found», así que colapsaba al 400 por defecto —contradiciendo el
    /// contrato del endpoint, que ya declaraba <c>.ProducesProblem(404)</c>—. Recorre endpoint + mapeador
    /// reales, verificando la clasificación por código con independencia del idioma del mensaje.
    /// </summary>
    [Fact]
    public async Task RemoveFeatureFlagCriteria_WhenCriteriaDoesNotExist_ShouldReturn404()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/feature-flags", new
        {
            systemSuiteId = "11111111-1111-1111-1111-111111111111",
            tenantId = (string?)null,
            flagCode = $"g104_criteria_{Guid.NewGuid():N}",
            flagType = "Boolean",
            flagTargets = "tenant-console",
            linkedResourceType = "Module",
            linkedResourceId = "33333333-3333-3333-3333-333333333333",
            rolloutPercentage = (int?)null,
        }, TestContext.Current.CancellationToken);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var createdPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var featureFlagId = createdPayload.RootElement.GetProperty("featureFlagId").GetGuid();

        var missingCriteriaId = Guid.NewGuid();
        var deleteResponse = await _client.DeleteAsync(
            $"/api/v1/feature-flags/{featureFlagId}/criteria/{missingCriteriaId}",
            TestContext.Current.CancellationToken);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
