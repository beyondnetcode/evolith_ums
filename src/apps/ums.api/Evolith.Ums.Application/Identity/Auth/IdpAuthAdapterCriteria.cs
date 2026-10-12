namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Shell.Factory criteria for resolving the correct IIdpAuthAdapter
/// based on the identity provider's strategy name.
/// </summary>
public sealed record IdpAuthAdapterCriteria(string StrategyName);
