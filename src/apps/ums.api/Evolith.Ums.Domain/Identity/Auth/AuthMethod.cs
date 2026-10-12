using Evolith.Ums.Domain.Identity.Tenant.IdentityProvider;

namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// The resolved authentication method for a tenant.
/// When Type == Local, Provider is null.
/// When Type == IDP, Provider holds the active IdentityProvider aggregate.
/// </summary>
public sealed record AuthMethod(
    AuthMethodType   Type,
    IdentityProvider? Provider = null)
{
    public static AuthMethod Local()               => new(AuthMethodType.Local);
    public static AuthMethod Idp(IdentityProvider p) => new(AuthMethodType.IDP, p);
}
