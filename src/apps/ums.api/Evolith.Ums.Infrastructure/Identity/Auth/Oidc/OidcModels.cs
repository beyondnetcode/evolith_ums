namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Endpoints OIDC del inquilino. Se leen de la configuración del proveedor
/// (<c>IdpConfiguration.ConfigPayload</c>), nunca se hardcodean (ADR-UMS-094).
/// </summary>
public sealed record OidcEndpoints(
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string JwksUri,
    string Issuer);

/// <summary>
/// Parámetros del cliente OIDC registrado ante el IdP del inquilino.
/// El <c>ClientSecret</c> proviene del secreto cifrado en reposo y nunca se
/// registra en logs, proyecciones ni grafo (ADR-UMS-094).
/// </summary>
public sealed record OidcClientSettings(
    string ClientId,
    string? ClientSecret,
    string RedirectUri,
    string Scopes = "openid email profile");

/// <summary>
/// Configuración OIDC completa resuelta para un proveedor del inquilino:
/// endpoints + parámetros de cliente.
/// </summary>
public sealed record OidcProviderConfig(
    OidcEndpoints Endpoints,
    OidcClientSettings Client);

/// <summary>
/// Petición de autorización construida por el adaptador (Authorization Code + PKCE).
/// El <c>State</c>, <c>Nonce</c> y <c>CodeVerifier</c> deben persistirse en la sesión
/// del lado servidor para validarse en el callback.
/// </summary>
public sealed record OidcAuthorizationRequest(
    string AuthorizationUrl,
    string State,
    string Nonce,
    string CodeVerifier,
    string CodeChallenge);

/// <summary>
/// Datos del retorno (callback) OIDC más los artefactos de sesión emitidos en la
/// fase de autorización. El adaptador valida que <c>State</c> coincida con
/// <c>ExpectedState</c> y que el <c>nonce</c> del <c>id_token</c> coincida con
/// <c>ExpectedNonce</c>.
/// </summary>
public sealed record OidcCallback(
    string Code,
    string State,
    string ExpectedState,
    string ExpectedNonce,
    string CodeVerifier,
    string RedirectUri);

/// <summary>
/// Respuesta del token endpoint del IdP tras el intercambio del código.
/// </summary>
public sealed record OidcTokenResponse(
    string? IdToken,
    string? AccessToken,
    string? RefreshToken,
    string? TokenType,
    int? ExpiresIn);

/// <summary>
/// Llave pública RSA publicada por el JWKS del issuer (subconjunto usado para
/// verificar la firma RS256 del <c>id_token</c>).
/// </summary>
public sealed record OidcJsonWebKey(
    string Kid,
    string Kty,
    string? Alg,
    string? Use,
    string N,
    string E);
