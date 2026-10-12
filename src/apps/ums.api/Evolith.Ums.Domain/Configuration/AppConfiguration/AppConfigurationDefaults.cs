namespace Evolith.Ums.Domain.Configuration.AppConfiguration;

public static class AppConfigurationDefaults
{
    public const int SessionTimeoutMinutes = 30;
    public const int MaxLoginAttempts = 5;
    // ADR-UMS-095: duración del bloqueo temporal tras alcanzar MaxLoginAttempts. Default seguro y conservador.
    public const int AccountLockoutDurationMinutes = 15;
    public const int AccessTokenDurationMs = 3_600_000;
    public const int RefreshTokenDurationMs = 604_800_000;
    public const int MinPasswordLength = 12;
    public const int MaxValidityPeriodDays = 365;
    public const bool MfaRequiredForAdmin = false;
    public const string MfaAllowedMethods = "Totp,WebAuthn,SmsOtp,EmailOtp";
    public const bool UiCustomBrandingEnabled = false;
    public const string FrontendConfigTransport = "rest";
    public const bool AuthUseExternalIdp = false;
    public const string UiLanguageDefault = "es";
    public const string UiTimezoneDefault = "America/Lima";

    // Refresh token (ADR-UMS-091). Fail-closed: deshabilitado por defecto.
    public const bool AuthRefreshTokenEnabled = false;
    public const int AuthRefreshTokenLifetimeMinutes = 43_200; // 30 días
    public const bool AuthRefreshTokenRotate = true;
    public const bool AuthRefreshTokenDetectReuse = true;
    public const int AuthRefreshTokenMaxRenewals = 0; // 0 = sin tope
}
