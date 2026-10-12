using PactNet.Verifier;
using Evolith.Ums.ContractTest.Infrastructure;

namespace Evolith.Ums.ContractTest.Provider;

/// <summary>
/// OPS-02: Provider verification tests for ums-api.
///
/// Starts the real API in InMemory mode (via ContractTestWebApplicationFactory) and
/// replays each pact file recorded by consumer tests, verifying that the real API
/// honours every interaction.
///
/// State handlers supply the preconditions that consumer interactions declare via
/// the "Given(…)" clause.  Each handler uses the InMemory stores so no database is
/// needed.
///
/// Run order (enforced by CI):
///   1. Consumer tests  → writes pacts/*.json
///   2. Provider tests  → reads pacts/*.json and verifies against live API
/// </summary>
[Collection("Provider")]
public sealed class UmsApiProviderTests : IClassFixture<ContractTestWebApplicationFactory>
{
    private readonly ContractTestWebApplicationFactory _factory;
    private readonly ITestOutputHelper                 _output;

    // Must match the directory where consumer tests write pacts.
    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    public UmsApiProviderTests(ContractTestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output  = output;
    }

    [Fact]
    [Trait("pact", "provider")]
    public void VerifyAllPacts_ForUmsWebApp()
    {
        // Load every pact where the consumer is "ums-web-app".
        var pactDir = new DirectoryInfo(PactsDir);
        if (!pactDir.Exists || !pactDir.EnumerateFiles("ums-web-app-ums-api.json").Any())
        {
            _output.WriteLine("No pact files found. Run consumer tests first.");
            return;
        }

        // Use a fixed local port for the real Kestrel host used by the native PactNet verifier.
        const int port    = 9321;
        var       baseUri = new Uri($"http://127.0.0.1:{port}");

        // Start the API on a real Kestrel port using the factory's HttpMessageHandler.
        using var httpClient = _factory.CreateDefaultClient(new Uri($"http://localhost"));

        // Create a delegating WebApplication on real Kestrel using the factory's pipeline.
        using var pactServer = PactKestrelServer.Start(_factory, port);

        _output.WriteLine($"[Provider] Verifying against: {baseUri}");

        var config = new PactVerifierConfig
        {
            Outputters = [new XunitOutput(_output)],
            LogLevel   = PactLogLevel.Warn,
        };

        new PactVerifier("ums-api", config)
            .WithHttpEndpoint(baseUri)
            .WithDirectorySource(pactDir)
            .WithProviderStateUrl(new Uri(baseUri, "/_pact/provider-states"))
            .Verify();
    }
}
