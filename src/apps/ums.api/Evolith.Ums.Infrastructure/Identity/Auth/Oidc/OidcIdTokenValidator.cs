using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Evolith.Ums.Domain.Identity.Auth;

namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Validación estricta del <c>id_token</c> OIDC y mapeo de claims → identidad UMS
/// (ADR-UMS-094). Verifica:
/// <list type="bullet">
///   <item>formato JWT y algoritmo RS256 (rechaza <c>none</c> y confusión de algoritmo);</item>
///   <item>firma RSASSA-PKCS1-v1_5/SHA-256 contra la llave del JWKS del issuer (por <c>kid</c>);</item>
///   <item>claims <c>iss</c>, <c>aud</c>, <c>exp</c>, <c>nbf</c> y <c>nonce</c>.</item>
/// </list>
/// Un adaptador laxo es una vulnerabilidad: cualquier discrepancia devuelve
/// <see cref="Result"/> de fallo con código de dominio, nunca una excepción.
/// </summary>
public sealed class OidcIdTokenValidator
{
    private const string ExpectedAlgorithm = "RS256";

    private readonly IJwksProvider _jwksProvider;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _clockSkew;

    public OidcIdTokenValidator(
        IJwksProvider jwksProvider,
        TimeProvider? timeProvider = null,
        TimeSpan? clockSkew = null)
    {
        _jwksProvider = jwksProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _clockSkew = clockSkew ?? TimeSpan.FromMinutes(2);
    }

    public async Task<Result<ExternalIdentity>> ValidateAsync(
        string idToken,
        OidcEndpoints endpoints,
        string expectedAudience,
        string expectedNonce,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.MalformedIdToken);
        }

        var parts = idToken.Split('.');
        if (parts.Length != 3)
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.MalformedIdToken);
        }

        JsonElement header;
        JsonElement payload;
        try
        {
            header  = ParseSegment(parts[0]);
            payload = ParseSegment(parts[1]);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.MalformedIdToken);
        }

        // 1) Algoritmo: sólo RS256 (rechaza 'none' y confusión de algoritmo HS/RS).
        var alg = ReadString(header, "alg");
        if (!string.Equals(alg, ExpectedAlgorithm, StringComparison.Ordinal))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.UnsupportedAlgorithm(alg));
        }

        // 2) Firma: llave por 'kid' desde el JWKS del issuer.
        var kid = ReadString(header, "kid");
        var keysResult = await _jwksProvider.GetSigningKeysAsync(endpoints, cancellationToken);
        if (keysResult.IsFailure)
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.JwksFetchFailed(keysResult.Error));
        }

        var key = SelectKey(keysResult.Value, kid);
        if (key is null)
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.SigningKeyNotFound(kid));
        }

        if (!VerifySignature(parts[0], parts[1], parts[2], key))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.InvalidSignature);
        }

        // 3) iss
        var iss = ReadString(payload, "iss");
        if (!string.Equals(iss, endpoints.Issuer, StringComparison.Ordinal))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.InvalidIssuer);
        }

        // 4) aud (string o arreglo)
        if (!AudienceMatches(payload, expectedAudience))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.InvalidAudience);
        }

        // 5) exp / 6) nbf
        var now = _timeProvider.GetUtcNow();
        if (TryReadUnixTime(payload, "exp", out var exp) && now > exp + _clockSkew)
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.Expired);
        }

        if (TryReadUnixTime(payload, "nbf", out var nbf) && now < nbf - _clockSkew)
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.NotYetValid);
        }

        // 7) nonce
        var nonce = ReadString(payload, "nonce");
        if (!string.IsNullOrEmpty(expectedNonce) && !string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.NonceMismatch);
        }

        // 8) Mapeo claims → identidad UMS.
        return MapIdentity(payload);
    }

    private static Result<ExternalIdentity> MapIdentity(JsonElement payload)
    {
        var email = ReadString(payload, "email");
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<ExternalIdentity>.Failure(OidcAuthErrors.MissingEmail);
        }

        var subject = ReadString(payload, "sub");
        var displayName = ReadString(payload, "name")
            ?? ReadString(payload, "preferred_username")
            ?? email.Split('@')[0];

        var claims = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in payload.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                claims[prop.Name] = prop.Value.GetString()!;
            }
            else if (prop.Value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                claims[prop.Name] = prop.Value.ToString();
            }
        }

        var identity = new ExternalIdentity(email, subject, displayName, claims);
        return Result<ExternalIdentity>.Success(identity);
    }

    private static bool VerifySignature(string headerSegment, string payloadSegment, string signatureSegment, OidcJsonWebKey key)
    {
        try
        {
            var signingInput = Encoding.ASCII.GetBytes($"{headerSegment}.{payloadSegment}");
            var signature = OidcBase64Url.Decode(signatureSegment);

            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus  = OidcBase64Url.Decode(key.N),
                Exponent = OidcBase64Url.Decode(key.E),
            });

            return rsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    private static OidcJsonWebKey? SelectKey(IReadOnlyList<OidcJsonWebKey> keys, string? kid)
    {
        var rsaKeys = keys.Where(k => string.Equals(k.Kty, "RSA", StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrEmpty(kid))
        {
            return rsaKeys.FirstOrDefault(k => string.Equals(k.Kid, kid, StringComparison.Ordinal));
        }

        // Sin 'kid': sólo es determinista si hay exactamente una llave RSA.
        return rsaKeys.Count == 1 ? rsaKeys[0] : null;
    }

    private static bool AudienceMatches(JsonElement payload, string expectedAudience)
    {
        if (!payload.TryGetProperty("aud", out var aud))
        {
            return false;
        }

        return aud.ValueKind switch
        {
            JsonValueKind.String => string.Equals(aud.GetString(), expectedAudience, StringComparison.Ordinal),
            JsonValueKind.Array  => aud.EnumerateArray().Any(a =>
                a.ValueKind == JsonValueKind.String &&
                string.Equals(a.GetString(), expectedAudience, StringComparison.Ordinal)),
            _ => false,
        };
    }

    private static bool TryReadUnixTime(JsonElement payload, string name, out DateTimeOffset value)
    {
        value = default;
        if (!payload.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (!element.TryGetInt64(out var seconds))
        {
            return false;
        }

        value = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return true;
    }

    private static JsonElement ParseSegment(string segment)
    {
        var json = Encoding.UTF8.GetString(OidcBase64Url.Decode(segment));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
