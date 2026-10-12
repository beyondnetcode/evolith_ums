namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// The authentication method determined from the tenant's
/// AUTH_USE_EXTERNAL_IDP configuration parameter.
/// </summary>
public enum AuthMethodType
{
    /// <summary>BCrypt password validation against the internal credential store.</summary>
    Local,

    /// <summary>Authentication delegated to a registered external Identity Provider.</summary>
    IDP
}
