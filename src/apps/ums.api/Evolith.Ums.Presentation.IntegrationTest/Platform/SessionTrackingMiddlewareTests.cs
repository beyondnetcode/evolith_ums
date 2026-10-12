using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Platform;

public sealed class SessionTrackingMiddlewareTests : IClassFixture<UmsApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SessionTrackingMiddlewareTests(UmsApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task Request_WithSessionTrackingIdHeader_ShouldEchoHeader()
    {
        const string sessionTrackingId = "session-abc-123";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(ObservabilityHeaders.SessionTrackingId, sessionTrackingId);

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues(ObservabilityHeaders.SessionTrackingId, out var values).Should().BeTrue();
        values!.Single().Should().Be(sessionTrackingId);
    }

    [Fact]
    public async Task Request_WithoutSessionTrackingIdHeader_ShouldGenerateHeader()
    {
        using var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues(ObservabilityHeaders.SessionTrackingId, out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }
}
