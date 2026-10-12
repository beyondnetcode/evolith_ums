namespace Evolith.Ums.Application.Test.Identity.Auth.Oidc;

using System.Net;
using System.Net.Http;
using Xunit;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// FR-042 · ADR-UMS-097 §2.3 · G-108 — pruebas de la SEPARACIÓN ESTRUCTURAL que hace
/// <see cref="HttpOidcTokenClient"/> al intercambiar el código en el token endpoint:
/// <list type="bullet">
///   <item><b>5xx / timeout / transporte</b> → indisponibilidad de INFRAESTRUCTURA → código
///     <c>AUTH_035</c>, que el <see cref="IdpAuthOutcomeClassifier"/> reconoce como infra y que
///     HABILITA el fallback encadenado.</item>
///   <item><b>4xx (invalid_grant/…)</b> → fallo de CREDENCIAL → código <c>AUTH_021</c>, que sigue
///     siendo TERMINAL (NUNCA infra): es la invariante anti <i>credential spraying</i> de ADR-UMS-097 §2.3.</item>
/// </list>
/// La distinción se hace por la CLASE de status HTTP / tipo de excepción, no por el texto del mensaje.
/// Todos los casos usan un <see cref="HttpMessageHandler"/> fake; ningún IdP real interviene.
/// </summary>
public sealed class HttpOidcTokenClientTests
{
    private static readonly OidcEndpoints Endpoints = OidcTestSupport.Endpoints();
    private static readonly OidcClientSettings Client =
        new("ums-client", "secreto", "https://ums.test/callback");

    private static HttpOidcTokenClient ClientWith(FakeTokenHttpHandler handler) =>
        new(new HttpClient(handler));

    private static Task<Result<OidcTokenResponse>> Exchange(HttpOidcTokenClient sut, CancellationToken ct = default) =>
        sut.ExchangeAuthorizationCodeAsync(
            Endpoints, Client, code: "auth-code-123", codeVerifier: "verifier-123",
            redirectUri: "https://ums.test/callback", ct);

    // ── Rama de INFRAESTRUCTURA: 5xx → AUTH_035 → clasifica como infra (habilita fallback) ────────

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]  // 500
    [InlineData(HttpStatusCode.BadGateway)]           // 502
    [InlineData(HttpStatusCode.ServiceUnavailable)]   // 503
    [InlineData(HttpStatusCode.GatewayTimeout)]       // 504
    public async Task Exchange_TokenEndpoint5xx_EmitsInfraCode_AUTH035(HttpStatusCode status)
    {
        var sut = ClientWith(FakeTokenHttpHandler.WithStatus(status, "{\"error\":\"server_error\"}"));

        var result = await Exchange(sut);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_035", result.Error);
        Assert.DoesNotContain("AUTH_021", result.Error);
        // Verificación de extremo a extremo: el código emitido lo trata el clasificador como infra.
        Assert.True(IdpAuthOutcomeClassifier.IsInfraUnavailable(result.Error));
    }

    [Fact]
    public async Task Exchange_TransportFailure_EmitsInfraCode_AUTH035()
    {
        // HttpRequestException = no se pudo completar el round-trip (conexión/red/DNS) → INFRA.
        var sut = ClientWith(FakeTokenHttpHandler.Throwing(new HttpRequestException("connection refused")));

        var result = await Exchange(sut);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_035", result.Error);
        Assert.True(IdpAuthOutcomeClassifier.IsInfraUnavailable(result.Error));
    }

    [Fact]
    public async Task Exchange_Timeout_EmitsInfraCode_AUTH035()
    {
        // Timeout del token endpoint: el HttpClient aborta con TaskCanceledException SIN que el token del
        // llamador esté cancelado → INFRA (no es una cancelación del llamador).
        var sut = ClientWith(FakeTokenHttpHandler.Throwing(new TaskCanceledException("timeout")));

        var result = await Exchange(sut, ct: default);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_035", result.Error);
        Assert.True(IdpAuthOutcomeClassifier.IsInfraUnavailable(result.Error));
    }

    // ── Rama de CREDENCIAL: 4xx → AUTH_021 → TERMINAL (NUNCA infra) — anti credential spraying ────

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}")]     // 400 invalid_grant (código inválido)
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_client\"}")]  // 401 invalid_client
    [InlineData(HttpStatusCode.Forbidden, "{\"error\":\"access_denied\"}")]      // 403
    public async Task Exchange_TokenEndpoint4xx_EmitsCredentialCode_AUTH021_NotInfra(HttpStatusCode status, string body)
    {
        var sut = ClientWith(FakeTokenHttpHandler.WithStatus(status, body));

        var result = await Exchange(sut);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_021", result.Error);
        Assert.DoesNotContain("AUTH_035", result.Error);
        // GUARDA ANTI-REGRESIÓN DE SEGURIDAD: un 4xx (fallo de credencial) JAMÁS debe clasificarse como
        // infra; hacerlo permitiría encadenar la misma credencial contra cada IdP (credential spraying
        // cross-IdP, ADR-UMS-097 §2.3). NO "arreglar" esta aserción debilitándola: la resiliencia se ganó
        // separando el 5xx en AUTH_035, no relajando el 4xx.
        Assert.False(IdpAuthOutcomeClassifier.IsInfraUnavailable(result.Error));
    }

    // ── Cuerpo 2xx malformado: la credencial fue aceptada pero no parsea → TERMINAL (fail-closed) ─

    [Fact]
    public async Task Exchange_Success2xx_MalformedJsonBody_EmitsTerminalCode_AUTH021_NotInfra()
    {
        var sut = ClientWith(FakeTokenHttpHandler.WithStatus(HttpStatusCode.OK, "esto-no-es-json"));

        var result = await Exchange(sut);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_021", result.Error);
        Assert.False(IdpAuthOutcomeClassifier.IsInfraUnavailable(result.Error));
    }

    // ── Cancelación del LLAMADOR: no es indisponibilidad del IdP → se propaga (no se enmascara) ───

    [Fact]
    public async Task Exchange_CallerCancels_PropagatesCancellation_NotInfra()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var sut = ClientWith(FakeTokenHttpHandler.HonoringCancellation());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Exchange(sut, cts.Token));
    }

    // ── Camino feliz: 2xx con JSON válido → id_token parseado ─────────────────────────────────────

    [Fact]
    public async Task Exchange_Success2xx_ValidJson_ReturnsToken()
    {
        const string body =
            "{\"id_token\":\"abc.def.ghi\",\"access_token\":\"at\",\"token_type\":\"Bearer\",\"expires_in\":300}";
        var sut = ClientWith(FakeTokenHttpHandler.WithStatus(HttpStatusCode.OK, body));

        var result = await Exchange(sut);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal("abc.def.ghi", result.Value.IdToken);
    }

    /// <summary>
    /// <see cref="HttpMessageHandler"/> fake que devuelve un status configurado, lanza una excepción
    /// de transporte/timeout, u honra la cancelación del token. Permite ejercer las ramas 5xx/4xx/2xx
    /// y de excepción de <see cref="HttpOidcTokenClient"/> sin un IdP real.
    /// </summary>
    private sealed class FakeTokenHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

        private FakeTokenHttpHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
            => _responder = responder;

        public static FakeTokenHttpHandler WithStatus(HttpStatusCode status, string body) =>
            new((_, _) => new HttpResponseMessage(status) { Content = new StringContent(body) });

        public static FakeTokenHttpHandler Throwing(Exception ex) =>
            new((_, _) => throw ex);

        public static FakeTokenHttpHandler HonoringCancellation() =>
            new((_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            });

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request, cancellationToken));
    }
}
