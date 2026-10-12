namespace Evolith.Ums.Sdk.Contracts;

/// <summary>
/// Canonical UMS error code catalog. Mirror of <c>src/libs/sdk/contracts/error-codes.yaml</c>.
/// New codes added via MINOR bump; existing codes never reused. See ADR-0073.
/// </summary>
public static class UmsErrorCodes
{
    // Authentication — server-emitted (AUTH_001..AUTH_010)
    public const string ValidationError = "AUTH_001";
    public const string TenantNotFound = "AUTH_002";
    public const string TenantNotActive = "AUTH_003";
    public const string IdpUserHasNoUmsAccount = "AUTH_004";
    public const string UserNotActive = "AUTH_005";
    public const string InvalidCredentials = "AUTH_006";
    public const string AccountLocked = "AUTH_007";
    public const string MfaChallengeRequired = "AUTH_008";
    public const string MfaChallengeFailed = "AUTH_009";
    public const string SessionExpired = "AUTH_010";

    // IDP resolution — server-emitted (AUTH_011..AUTH_019)
    public const string NoActiveIdpConfigured = "AUTH_011";
    public const string NoIdpAdapterRegistered = "AUTH_012";
    public const string IdpCallFailed = "AUTH_013";
    public const string IdpTokenValidationFailed = "AUTH_014";

    // Authorization — SDK-emitted (AUTH_100..AUTH_199)
    public const string ScopeNotGranted = "AUTH_101";
    public const string ScopeDenied = "AUTH_102";
    public const string MenuOptionNotGranted = "AUTH_103";
    public const string MenuOptionDenied = "AUTH_104";
    public const string DomainAccessNotGranted = "AUTH_105";
    public const string DomainAccessDenied = "AUTH_106";
    public const string FeatureFlagDisabled = "AUTH_107";
    public const string FeatureFlagNotFound = "AUTH_108";
    public const string TenantMismatch = "AUTH_109";

    // Graph lifecycle — SDK-emitted (AUTH_200..AUTH_299)
    public const string AuthGraphExpired = "AUTH_201";
    public const string AuthGraphMissing = "AUTH_202";
    public const string AuthGraphMalformed = "AUTH_203";
    public const string AuthGraphSchemaMissing = "AUTH_204";
    public const string AuthGraphSchemaUnsupported = "AUTH_205";
}
