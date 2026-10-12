using System.Text.Json;

namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Parsea la configuración OIDC (endpoints + cliente) desde el <c>ConfigPayload</c>
/// JSON del <c>IdpConfiguration</c> del inquilino. No hardcodea endpoints (ADR-UMS-094).
///
/// Acepta claves tanto en estilo OIDC discovery (<c>authorization_endpoint</c>,
/// <c>token_endpoint</c>, <c>jwks_uri</c>, <c>issuer</c>) como abreviadas. Si el
/// payload sólo trae <c>issuer</c>/<c>authority</c>, deriva los endpoints estándar
/// de Keycloak/OIDC.
/// </summary>
public static class OidcProviderConfigParser
{
    public static Result<OidcProviderConfig> Parse(string configPayload, string? clientSecret = null)
    {
        if (string.IsNullOrWhiteSpace(configPayload))
        {
            return Result<OidcProviderConfig>.Failure(
                OidcAuthErrors.ConfigNotFound("El 'configPayload' del proveedor está vacío."));
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(configPayload);
        }
        catch (JsonException)
        {
            return Result<OidcProviderConfig>.Failure(
                OidcAuthErrors.ConfigNotFound("El 'configPayload' del proveedor no es JSON válido."));
        }

        using (doc)
        {
            var root = doc.RootElement;

            var issuer = Read(root, "issuer", "authority");
            var authorization = Read(root, "authorization_endpoint", "authorizationEndpoint");
            var token = Read(root, "token_endpoint", "tokenEndpoint");
            var jwks = Read(root, "jwks_uri", "jwksUri", "jwks");

            // Derivación estándar OIDC/Keycloak a partir del issuer si faltan endpoints.
            if (!string.IsNullOrWhiteSpace(issuer))
            {
                var baseUrl = issuer!.TrimEnd('/');
                authorization ??= $"{baseUrl}/protocol/openid-connect/auth";
                token ??= $"{baseUrl}/protocol/openid-connect/token";
                jwks ??= $"{baseUrl}/protocol/openid-connect/certs";
            }

            var clientId = Read(root, "client_id", "clientId", "applicationId");
            var redirectUri = Read(root, "redirect_uri", "redirectUri");
            var scopes = Read(root, "scope", "scopes") ?? "openid email profile";
            var secret = clientSecret ?? Read(root, "client_secret", "clientSecret");

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(issuer)) missing.Add("issuer");
            if (string.IsNullOrWhiteSpace(authorization)) missing.Add("authorization_endpoint");
            if (string.IsNullOrWhiteSpace(token)) missing.Add("token_endpoint");
            if (string.IsNullOrWhiteSpace(jwks)) missing.Add("jwks_uri");
            if (string.IsNullOrWhiteSpace(clientId)) missing.Add("client_id");
            if (string.IsNullOrWhiteSpace(redirectUri)) missing.Add("redirect_uri");

            if (missing.Count > 0)
            {
                return Result<OidcProviderConfig>.Failure(
                    OidcAuthErrors.ConfigNotFound($"Faltan claves obligatorias: {string.Join(", ", missing)}."));
            }

            var config = new OidcProviderConfig(
                new OidcEndpoints(authorization!, token!, jwks!, issuer!),
                new OidcClientSettings(clientId!, secret, redirectUri!, scopes));

            return Result<OidcProviderConfig>.Success(config);
        }
    }

    private static string? Read(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }
        }

        return null;
    }
}
