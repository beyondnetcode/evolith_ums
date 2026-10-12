using System.Net.Http;
using System.Text.Json;

namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Implementación HTTP real de <see cref="IOidcTokenClient"/>: intercambia el
/// authorization code por tokens contra el token endpoint del IdP
/// (<c>grant_type=authorization_code</c> + PKCE <c>code_verifier</c>).
/// Usa <c>client_secret_post</c> cuando el cliente es confidencial.
///
/// En los unit tests este cliente se sustituye por un fake; el IdP real (Keycloak)
/// se ejerce sin mocks en el arnés de integración (slice 2, ADR-UMS-094).
/// </summary>
public sealed class HttpOidcTokenClient : IOidcTokenClient
{
    private readonly HttpClient _httpClient;

    public HttpOidcTokenClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<OidcTokenResponse>> ExchangeAuthorizationCodeAsync(
        OidcEndpoints endpoints,
        OidcClientSettings client,
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type",    "authorization_code"),
            new("code",          code),
            new("redirect_uri",  redirectUri),
            new("client_id",     client.ClientId),
            new("code_verifier", codeVerifier),
        };

        if (!string.IsNullOrEmpty(client.ClientSecret))
        {
            form.Add(new KeyValuePair<string, string>("client_secret", client.ClientSecret));
        }

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await _httpClient.PostAsync(endpoints.TokenEndpoint, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // G-108 · ADR-UMS-097 §2.3 — separación ESTRUCTURAL por clase de status HTTP (NUNCA por texto):
                //   • 5xx  → INDISPONIBILIDAD de infraestructura del IdP → AUTH_035 (TokenEndpointUnavailable).
                //            Entra en la lista blanca del clasificador y HABILITA el fallback encadenado.
                //   • 4xx  → fallo de credencial/petición (p. ej. invalid_grant) → AUTH_021 (TokenExchangeFailed),
                //            TERMINAL. NUNCA debe entrar en la lista blanca de infra: encadenar ante un 4xx
                //            abriría credential spraying cross-IdP (invariante irrenunciable de ADR-UMS-097 §2.3).
                //   • Cualquier otro no-éxito (p. ej. 3xx inesperado) → TERMINAL (fail-closed: ante la duda,
                //            no se avanza la cadena; errar hacia terminal es lo correcto).
                return (int)response.StatusCode >= 500
                    ? Result<OidcTokenResponse>.Failure(
                        OidcAuthErrors.TokenEndpointUnavailable($"HTTP {(int)response.StatusCode}."))
                    : Result<OidcTokenResponse>.Failure(
                        OidcAuthErrors.TokenExchangeFailed($"HTTP {(int)response.StatusCode}."));
            }

            var token = Parse(body);
            return Result<OidcTokenResponse>.Success(token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancelación pedida por el LLAMADOR (no es indisponibilidad del IdP): se propaga tal cual.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // Fallo de TRANSPORTE (conexión/red/DNS) o TIMEOUT del token endpoint (el HttpClient aborta con
            // TaskCanceledException —derivada de OperationCanceledException— sin que el token del llamador
            // esté cancelado) → INDISPONIBILIDAD de infraestructura → AUTH_035 (habilita el fallback).
            return Result<OidcTokenResponse>.Failure(
                OidcAuthErrors.TokenEndpointUnavailable("No se pudo contactar el token endpoint (transporte/timeout)."));
        }
        catch (JsonException)
        {
            // Respuesta 2xx con cuerpo NO-JSON: la credencial fue aceptada, pero el cuerpo no parsea. No es
            // ni un rechazo de credencial ni una indisponibilidad inequívoca → TERMINAL (fail-closed): no se
            // avanza la cadena por una anomalía de formato de una respuesta por lo demás exitosa.
            return Result<OidcTokenResponse>.Failure(
                OidcAuthErrors.TokenExchangeFailed("La respuesta del token endpoint no es un JSON válido."));
        }
    }

    private static OidcTokenResponse Parse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        return new OidcTokenResponse(
            IdToken:      ReadString(root, "id_token"),
            AccessToken:  ReadString(root, "access_token"),
            RefreshToken: ReadString(root, "refresh_token"),
            TokenType:    ReadString(root, "token_type"),
            ExpiresIn:    root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var v) ? v : null);
    }

    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
