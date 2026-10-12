namespace Evolith.Ums.Sdk.Authorization;

/// <summary>
/// Thrown by the aspect or guard when an authorization decision denies access and the configured
/// behavior is to throw. Carries the full <see cref="AuthorizationDecision"/> for diagnostics.
/// </summary>
public sealed class AuthorizationDeniedException : UnauthorizedAccessException
{
    public AuthorizationDecision Decision { get; }

    public AuthorizationDeniedException(AuthorizationDecision decision)
        : base(decision.Reason ?? "Authorization denied.")
    {
        Decision = decision;
    }
}
