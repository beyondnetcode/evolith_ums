using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Puerto de acceso HTTP al token endpoint del IdP. Se abstrae para poder
/// unit-testear la orquestación del adaptador sin un IdP vivo (ADR-UMS-094 · slice 1).
/// La implementación real (HTTP) es <see cref="HttpOidcTokenClient"/>.
/// </summary>
public interface IOidcTokenClient
{
    Task<Result<OidcTokenResponse>> ExchangeAuthorizationCodeAsync(
        OidcEndpoints endpoints,
        OidcClientSettings client,
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Puerto de obtención del JWKS (llaves públicas de firma) del issuer. Se abstrae
/// para validar la firma del <c>id_token</c> con llaves controladas en los unit
/// tests, sin depender de un IdP real (ADR-UMS-094 · slice 1).
/// La implementación real (HTTP) es <see cref="HttpJwksProvider"/>.
/// </summary>
public interface IJwksProvider
{
    Task<Result<IReadOnlyList<OidcJsonWebKey>>> GetSigningKeysAsync(
        OidcEndpoints endpoints,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Puerto que resuelve los endpoints y parámetros OIDC del proveedor a partir de la
/// configuración del inquilino (<c>IdpConfiguration</c>), de modo que el adaptador
/// nunca hardcodee endpoints (ADR-UMS-094). La implementación real es
/// <see cref="IdpConfigurationOidcProviderConfigStore"/>.
/// </summary>
public interface IOidcProviderConfigStore
{
    Task<Result<OidcProviderConfig>> GetAsync(
        IdentityProvider provider,
        CancellationToken cancellationToken = default);
}
