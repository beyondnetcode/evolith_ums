using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// Orchestrates authentication against an external Identity Provider.
/// Dispatches to the correct IIdpAuthAdapter based on the provider's strategy
/// (AzureAd, Okta, GenericOidc, etc.) via Shell.Factory.
/// </summary>
public interface IIdpAuthStrategy
{
    Task<Result<ExternalIdentity>> AuthenticateAsync(
        Guid              tenantId,
        string            credential,
        IdentityProvider  provider,
        CancellationToken cancellationToken = default);
}
