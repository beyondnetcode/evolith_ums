namespace Evolith.Ums.Application.Test.Identity.Auth.Oidc;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Utilidades de prueba para el flujo OIDC: generación de llaves RSA de prueba,
/// construcción de <c>id_token</c> firmados y fakes de los puertos HTTP/JWKS.
/// Ningún test toca un IdP real (ADR-UMS-094 · slice 1).
/// </summary>
internal static class OidcTestSupport
{
    public static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static OidcEndpoints Endpoints(string issuer = "https://idp.test/realms/evolith") =>
        new(
            AuthorizationEndpoint: $"{issuer}/protocol/openid-connect/auth",
            TokenEndpoint:         $"{issuer}/protocol/openid-connect/token",
            JwksUri:               $"{issuer}/protocol/openid-connect/certs",
            Issuer:                issuer);

    public static OidcJsonWebKey PublicJwk(RSA rsa, string kid)
    {
        var p = rsa.ExportParameters(includePrivateParameters: false);
        return new OidcJsonWebKey(
            Kid: kid,
            Kty: "RSA",
            Alg: "RS256",
            Use: "sig",
            N:   Base64Url(p.Modulus!),
            E:   Base64Url(p.Exponent!));
    }

    /// <summary>Construye un id_token RS256 firmado con <paramref name="signingKey"/>.</summary>
    public static string BuildIdToken(
        RSA signingKey,
        string kid,
        IReadOnlyDictionary<string, object> payload,
        string alg = "RS256")
    {
        var header = new Dictionary<string, object> { ["alg"] = alg, ["typ"] = "JWT" };
        if (kid is not null)
        {
            header["kid"] = kid;
        }

        var headerSegment  = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)));
        var payloadSegment = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signingInput   = Encoding.ASCII.GetBytes($"{headerSegment}.{payloadSegment}");

        byte[] signature = alg switch
        {
            "none" => Array.Empty<byte>(),
            "HS256" => new HMACSHA256(Encoding.UTF8.GetBytes("client-secret")).ComputeHash(signingInput),
            _ => signingKey.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
        };

        return $"{headerSegment}.{payloadSegment}.{Base64Url(signature)}";
    }

    public static Dictionary<string, object> ValidPayload(
        string issuer = "https://idp.test/realms/evolith",
        string audience = "ums-client",
        string nonce = "nonce-123",
        string email = "user@ransa.pe",
        long? exp = null,
        long? nbf = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new Dictionary<string, object>
        {
            ["iss"]                = issuer,
            ["aud"]                = audience,
            ["exp"]                = exp ?? now + 300,
            ["nbf"]                = nbf ?? now - 60,
            ["iat"]                = now,
            ["nonce"]              = nonce,
            ["sub"]                = "idp|abc-123",
            ["email"]              = email,
            ["email_verified"]     = true,
            ["name"]               = "Ana Ransa",
            ["preferred_username"] = "aransa",
        };
    }
}

/// <summary>Fake de <see cref="IJwksProvider"/> con llaves controladas.</summary>
internal sealed class FakeJwksProvider : IJwksProvider
{
    private readonly Result<IReadOnlyList<OidcJsonWebKey>> _result;

    private FakeJwksProvider(Result<IReadOnlyList<OidcJsonWebKey>> result) => _result = result;

    public static FakeJwksProvider WithKeys(params OidcJsonWebKey[] keys) =>
        new(Result<IReadOnlyList<OidcJsonWebKey>>.Success(keys));

    public static FakeJwksProvider Failing(string error) =>
        new(Result<IReadOnlyList<OidcJsonWebKey>>.Failure(error));

    public Task<Result<IReadOnlyList<OidcJsonWebKey>>> GetSigningKeysAsync(
        OidcEndpoints endpoints, CancellationToken cancellationToken = default)
        => Task.FromResult(_result);
}

/// <summary>Fake de <see cref="IOidcTokenClient"/> con respuesta controlada.</summary>
internal sealed class FakeOidcTokenClient : IOidcTokenClient
{
    private readonly Result<OidcTokenResponse> _result;

    private FakeOidcTokenClient(Result<OidcTokenResponse> result) => _result = result;

    public static FakeOidcTokenClient Returning(string? idToken) =>
        new(Result<OidcTokenResponse>.Success(new OidcTokenResponse(idToken, "access", null, "Bearer", 300)));

    public static FakeOidcTokenClient Failing(string error) =>
        new(Result<OidcTokenResponse>.Failure(error));

    public Task<Result<OidcTokenResponse>> ExchangeAuthorizationCodeAsync(
        OidcEndpoints endpoints, OidcClientSettings client, string code, string codeVerifier,
        string redirectUri, CancellationToken cancellationToken = default)
        => Task.FromResult(_result);
}

/// <summary>Fake de <see cref="IOidcProviderConfigStore"/>.</summary>
internal sealed class FakeOidcProviderConfigStore : IOidcProviderConfigStore
{
    private readonly Result<OidcProviderConfig> _result;

    private FakeOidcProviderConfigStore(Result<OidcProviderConfig> result) => _result = result;

    public static FakeOidcProviderConfigStore Returning(OidcProviderConfig config) =>
        new(Result<OidcProviderConfig>.Success(config));

    public static FakeOidcProviderConfigStore Failing(string error) =>
        new(Result<OidcProviderConfig>.Failure(error));

    public Task<Result<OidcProviderConfig>> GetAsync(
        Evolith.Ums.Domain.Identity.Tenant.IdentityProvider.IdentityProvider provider,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_result);
}

/// <summary><see cref="TimeProvider"/> fijo para probar exp/nbf de forma determinista.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;
    public FixedTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
}
