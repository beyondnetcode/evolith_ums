using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// ACL anti-corruption port for delegating credential validation to an
/// external Identity Provider.
///
/// Concrete adapters are registered via Shell.Factory (same pattern as
/// IdpResolutionStrategyFactorySetup). In development, StubIdpAuthAdapter
/// is used. Production adapters (AzureAd, Okta, GenericOidc, etc.) are
/// registered when those integrations are implemented.
/// </summary>
public interface IIdpAuthAdapter
{
    /// <summary>
    /// Validates the provided credential/token with the external IDP.
    /// Returns the asserted external identity on success.
    /// </summary>
    Task<Result<ExternalIdentity>> ValidateAsync(
        IdentityProvider  provider,
        string            credential,
        CancellationToken cancellationToken = default);
}
