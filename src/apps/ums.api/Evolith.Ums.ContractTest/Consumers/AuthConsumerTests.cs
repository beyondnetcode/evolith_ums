using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Evolith.Ums.ContractTest.Infrastructure;
using PactNet;
using PactNet.Matchers;

namespace Evolith.Ums.ContractTest.Consumers;

/// <summary>
/// OPS-02: Consumer contract tests for the Auth API.
///
/// El endpoint real de inicio de sesión es <c>POST /api/v1/auth/login</c> y recibe
/// <c>{ tenantCode, username, password }</c> (ADR-0096 / AuthenticateUserCommand). Aquí sólo se
/// contrasta la FORMA HTTP de las rutas de rechazo, que no requieren sembrar un grafo de
/// autorización completo. El camino feliz (200 con el grafo + cookie de sesión) depende de un
/// grafo sembrado que excede el alcance de un contrato de forma y no se modela aquí.
///
/// <para>El cuerpo de error NO es ProblemDetails: es <c>LoginErrorResponse</c>
/// —<c>{ code, message, supportReferenceId }</c>—. El pacto declaraba <c>{ status, title }</c>, la
/// forma por defecto de ASP.NET, contra la que esta API dejó de responder; el `supportReferenceId`
/// es deliberado y es lo único que permite cruzar la queja de una persona con la traza del
/// servidor, así que el contrato se corrige hacia la API y no al revés.</para>
///
/// <para>El <c>Content-Type</c> se afirma por valor exacto. Con <c>Match.Type</c> —una plantilla de
/// cadena— la verificación pasaba con CUALQUIER content type, incluido el que la API dejó de
/// enviar: la comprobación existía y no comprobaba nada.</para>
/// </summary>
public sealed class AuthConsumerTests : IDisposable
{
    private readonly IPactBuilderV4 _pactBuilder;

    private static readonly string PactsDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "pacts");

    public AuthConsumerTests(ITestOutputHelper output)
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
    // POST /api/v1/auth/login — campos obligatorios ausentes → 400
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task PostLogin_WithMissingFields_Returns400()
    {
        _pactBuilder
            .UponReceiving("a login request that is missing required fields")
            .WithRequest(HttpMethod.Post, "/api/v1/auth/login")
            .WithHeader("Content-Type", Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithJsonBody(new
            {
                tenantCode = "",
                username   = "",
                password   = "",
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.BadRequest)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                code               = "AUTH_001",
                message            = Match.Type("Tenant code, username and password are required."),
                supportReferenceId = Match.Type("TX-2026-000001"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };

            using var content = JsonContent.Create(new
            {
                tenantCode = "",
                username   = "",
                password   = "",
            });

            var response = await client.PostAsync("/api/v1/auth/login", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/auth/login — credenciales no autenticables → 400
    // ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("pact", "consumer")]
    public async Task PostLogin_WithUnauthenticableCredentials_Returns400()
    {
        _pactBuilder
            .UponReceiving("a login request with credentials that cannot be authenticated")
            .Given("no tenant with code UNKNOWN_TENANT exists")
            .WithRequest(HttpMethod.Post, "/api/v1/auth/login")
            .WithHeader("Content-Type", Match.Regex("application/json; charset=utf-8", "application/json.*"))
            .WithJsonBody(new
            {
                tenantCode = "UNKNOWN_TENANT",
                username   = Match.Type("user@example.com"),
                password   = Match.Type("ValidPassword123!"),
            })
            .WillRespond()
            .WithStatus(HttpStatusCode.BadRequest)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                code               = "AUTH_002",
                message            = Match.Type("No pudimos iniciar sesión. Verifique el código del tenant."),
                supportReferenceId = Match.Type("TX-2026-000001"),
            });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            using var client = new HttpClient { BaseAddress = ctx.MockServerUri };

            using var content = JsonContent.Create(new
            {
                tenantCode = "UNKNOWN_TENANT",
                username   = "user@example.com",
                password   = "ValidPassword123!",
            });

            var response = await client.PostAsync("/api/v1/auth/login", content);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        });
    }

    public void Dispose() { }
}
