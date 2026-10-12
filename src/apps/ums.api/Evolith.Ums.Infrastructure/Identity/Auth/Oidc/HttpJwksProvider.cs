using System.Net.Http;
using System.Text.Json;

namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Implementación HTTP real de <see cref="IJwksProvider"/>: obtiene el documento
/// JWKS del issuer y extrae las llaves RSA de firma. En los unit tests se sustituye
/// por un fake con llaves controladas; el JWKS real se ejerce en el arnés Keycloak
/// (slice 2, ADR-UMS-094).
/// </summary>
public sealed class HttpJwksProvider : IJwksProvider
{
    private readonly HttpClient _httpClient;

    public HttpJwksProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<IReadOnlyList<OidcJsonWebKey>>> GetSigningKeysAsync(
        OidcEndpoints endpoints,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(endpoints.JwksUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<OidcJsonWebKey>>.Failure(
                    OidcAuthErrors.JwksFetchFailed($"HTTP {(int)response.StatusCode}."));
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var keys = ParseKeys(body);
            return Result<IReadOnlyList<OidcJsonWebKey>>.Success(keys);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Result<IReadOnlyList<OidcJsonWebKey>>.Failure(
                OidcAuthErrors.JwksFetchFailed("No se pudo contactar el endpoint JWKS."));
        }
    }

    private static IReadOnlyList<OidcJsonWebKey> ParseKeys(string body)
    {
        var keys = new List<OidcJsonWebKey>();

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("keys", out var keysElement) || keysElement.ValueKind != JsonValueKind.Array)
        {
            return keys;
        }

        foreach (var key in keysElement.EnumerateArray())
        {
            var kty = ReadString(key, "kty");
            var n = ReadString(key, "n");
            var e = ReadString(key, "e");

            if (!string.Equals(kty, "RSA", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(n) || string.IsNullOrEmpty(e))
            {
                continue;
            }

            keys.Add(new OidcJsonWebKey(
                Kid: ReadString(key, "kid") ?? string.Empty,
                Kty: kty!,
                Alg: ReadString(key, "alg"),
                Use: ReadString(key, "use"),
                N:   n!,
                E:   e!));
        }

        return keys;
    }

    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
