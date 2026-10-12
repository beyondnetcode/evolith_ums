using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Authorization;

public sealed class SystemSuiteValidationResponseTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SystemSuiteValidationResponseTests(UmsApiWebApplicationFactory factory)
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
        _client.DefaultRequestHeaders.Add("X-Language", "es");
    }

    [Fact]
    public async Task AddModule_DescriptionTooLong_ReturnsActionableMessageWithoutTechnicalDetails()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/system-suites/{Guid.NewGuid()}/modules",
            new
            {
                code = "SECURITY",
                name = "Security",
                description = new string('x', 501),
                sortOrder = 1,
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var problem = document.RootElement;

        problem.GetProperty("detail").GetString()
            .Should().NotBeNullOrWhiteSpace("validation message should describe the error");
        problem.GetRawText().Should().NotContain("stackTrace");
    }

    [Fact]
    public async Task AddModule_InvalidCode_ReturnsFieldRuleValueAndCorrectionHint()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/system-suites/{Guid.NewGuid()}/modules",
            new
            {
                code = "DDDD-!",
                name = "Security",
                description = "Safe description",
                sortOrder = 1,
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        document.RootElement.GetProperty("detail").GetString()
            .Should().Contain("formato inválido")
            .And.Contain("REPORTS_01");
    }
}
