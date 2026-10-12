namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Construye la URL de autorización del flujo Authorization Code + PKCE (S256),
/// emitiendo los artefactos de sesión (state, nonce, code_verifier) que deben
/// persistirse para validarse en el callback (ADR-UMS-094).
/// </summary>
public sealed class OidcAuthorizationRequestFactory
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Minor Code Smell", "S2325:Methods and properties that don't access instance data should be static",
        Justification = "Servicio registrado en DI (AddScoped) e invocado por instancia; la API de fábrica " +
                        "es de instancia por diseño (ADR-UMS-094). Volverla estática rompería los llamadores.")]
    public OidcAuthorizationRequest Build(OidcProviderConfig config, string? loginHint = null)
    {
        var codeVerifier  = OidcPkce.GenerateCodeVerifier();
        var codeChallenge = OidcPkce.ComputeS256Challenge(codeVerifier);
        var state         = OidcPkce.GenerateOpaqueValue();
        var nonce         = OidcPkce.GenerateOpaqueValue();

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("response_type",         "code"),
            new("client_id",             config.Client.ClientId),
            new("redirect_uri",          config.Client.RedirectUri),
            new("scope",                 config.Client.Scopes),
            new("state",                 state),
            new("nonce",                 nonce),
            new("code_challenge",        codeChallenge),
            new("code_challenge_method", "S256"),
        };

        if (!string.IsNullOrWhiteSpace(loginHint))
        {
            parameters.Add(new KeyValuePair<string, string>("login_hint", loginHint));
        }

        var query = string.Join(
            '&',
            parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        var separator = config.Endpoints.AuthorizationEndpoint.Contains('?') ? '&' : '?';
        var url = $"{config.Endpoints.AuthorizationEndpoint}{separator}{query}";

        return new OidcAuthorizationRequest(url, state, nonce, codeVerifier, codeChallenge);
    }
}
