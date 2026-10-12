namespace Evolith.Ums.Sdk.Authorization.Aop;

/// <summary>
/// How the authorization aspect should react when a decision denies access.
/// </summary>
public enum DenialBehavior
{
    /// <summary>Throw <see cref="AuthorizationDeniedException"/>. Default. Maps to HTTP 403 in most frameworks.</summary>
    Throw = 0,

    /// <summary>Return <c>Result.Failure(errorCode, reason)</c>. Method must return Result or Task&lt;Result&gt;.</summary>
    ReturnFailure = 1
}
