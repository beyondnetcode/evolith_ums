namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Effective configuration values for this tenant, resolved from the
/// IConfigurationProvider with tenant-level overrides taking precedence
/// over global defaults. Clients receive these so they can apply the same
/// rules (session timeout, MFA policy, etc.) without re-querying UMS.
/// </summary>
public sealed record GraphEffectiveConfig(
    int  SessionTimeoutMinutes,
    int  MaxLoginAttempts,
    int  MinPasswordLength,
    bool MfaRequiredForAdmin,
    IReadOnlyCollection<string> MfaAllowedMethods,
    int  AccessTokenDurationMs,
    bool AuthUseExternalIdp);
