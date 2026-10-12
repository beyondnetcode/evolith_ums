namespace Evolith.Ums.Application.Test.Identity.Auth.Oidc;

using System.Security.Cryptography;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;
using Xunit;

/// <summary>
/// Unit tests de la validación estricta del <c>id_token</c> (ADR-UMS-094 · G-049).
/// Verifica éxito y, sobre todo, los negativos de seguridad (firma, exp, aud, nonce,
/// iss, alg) con llaves RSA generadas en el propio test y un JWKS fake — nunca un IdP
/// real (eso es el arnés Keycloak, slice 2).
/// </summary>
public sealed class OidcIdTokenValidatorTests
{
    private const string Kid = "test-key-1";
    private const string Audience = "ums-client";
    private const string Nonce = "nonce-123";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly OidcEndpoints _endpoints = OidcTestSupport.Endpoints();

    private static OidcIdTokenValidator CreateSut(IJwksProvider jwks, TimeProvider? clock = null)
        => new(jwks, clock ?? TimeProvider.System);

    private string SignValid(RSA signingKey, string alg = "RS256")
        => OidcTestSupport.BuildIdToken(
            signingKey, Kid,
            OidcTestSupport.ValidPayload(_endpoints.Issuer, Audience, Nonce),
            alg);

    // ── Éxito + mapeo de claims ─────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_ValidToken_ReturnsSuccessWithMappedIdentity()
    {
        var jwks  = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var token = SignValid(_rsa);

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal("user@ransa.pe", result.Value.Email);
        Assert.Equal("idp|abc-123", result.Value.ExternalId);
        Assert.Equal("Ana Ransa", result.Value.DisplayName);
        Assert.Equal("user@ransa.pe", result.Value.Claims["email"]);
        Assert.Equal("aransa", result.Value.Claims["preferred_username"]);
    }

    // ── Negativos: firma ────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_SignatureFromDifferentKey_ReturnsFailure_AUTH026()
    {
        using var attacker = RSA.Create(2048);
        var jwks  = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid)); // publica la buena
        var token = SignValid(attacker); // firmada con otra llave

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_026", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_AlgorithmNone_ReturnsFailure_AUTH024()
    {
        var jwks  = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var token = SignValid(_rsa, alg: "none");

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_024", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_AlgorithmConfusionHs256_ReturnsFailure_AUTH024()
    {
        // Confusión de algoritmo: un atacante firma HS256 con el 'client secret'.
        var jwks  = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var token = SignValid(_rsa, alg: "HS256");

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_024", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_UnknownKid_ReturnsFailure_AUTH025()
    {
        var jwks  = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, "otro-kid"));
        var token = SignValid(_rsa);

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_025", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_JwksFetchFails_ReturnsFailure_AUTH034()
    {
        var jwks  = FakeJwksProvider.Failing("boom");
        var token = SignValid(_rsa);

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_034", result.Error);
    }

    // ── Negativos: claims ───────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Expired_ReturnsFailure_AUTH029()
    {
        var jwks = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var past = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
        var token = OidcTestSupport.BuildIdToken(
            _rsa, Kid,
            OidcTestSupport.ValidPayload(_endpoints.Issuer, Audience, Nonce, exp: past, nbf: past - 60));

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_029", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_NotYetValid_ReturnsFailure_AUTH030()
    {
        var jwks   = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var future = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
        var token  = OidcTestSupport.BuildIdToken(
            _rsa, Kid,
            OidcTestSupport.ValidPayload(_endpoints.Issuer, Audience, Nonce, exp: future + 300, nbf: future));

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_030", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WrongAudience_ReturnsFailure_AUTH028()
    {
        var jwks  = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var token = OidcTestSupport.BuildIdToken(
            _rsa, Kid,
            OidcTestSupport.ValidPayload(_endpoints.Issuer, audience: "otro-cliente", nonce: Nonce));

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_028", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WrongIssuer_ReturnsFailure_AUTH027()
    {
        var jwks  = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var token = OidcTestSupport.BuildIdToken(
            _rsa, Kid,
            OidcTestSupport.ValidPayload(issuer: "https://malicioso.test", audience: Audience, nonce: Nonce));

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_027", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_NonceMismatch_ReturnsFailure_AUTH031()
    {
        var jwks  = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var token = OidcTestSupport.BuildIdToken(
            _rsa, Kid,
            OidcTestSupport.ValidPayload(_endpoints.Issuer, Audience, nonce: "nonce-del-atacante"));

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, expectedNonce: Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_031", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_MissingEmail_ReturnsFailure_AUTH032()
    {
        var jwks    = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var payload = OidcTestSupport.ValidPayload(_endpoints.Issuer, Audience, Nonce);
        payload.Remove("email");
        var token = OidcTestSupport.BuildIdToken(_rsa, Kid, payload);

        var result = await CreateSut(jwks).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_032", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_MalformedToken_ReturnsFailure_AUTH023()
    {
        var jwks = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));

        var result = await CreateSut(jwks).ValidateAsync("no-es-un-jwt", _endpoints, Audience, Nonce);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_023", result.Error);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredButWithinClockSkew_ReturnsSuccess()
    {
        // exp hace 30s, con skew por defecto de 2 min → aún válido.
        var jwks = FakeJwksProvider.WithKeys(OidcTestSupport.PublicJwk(_rsa, Kid));
        var now  = DateTimeOffset.UtcNow;
        var token = OidcTestSupport.BuildIdToken(
            _rsa, Kid,
            OidcTestSupport.ValidPayload(
                _endpoints.Issuer, Audience, Nonce,
                exp: now.ToUnixTimeSeconds() - 30));

        var clock  = new FixedTimeProvider(now);
        var result = await CreateSut(jwks, clock).ValidateAsync(token, _endpoints, Audience, Nonce);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
    }
}
