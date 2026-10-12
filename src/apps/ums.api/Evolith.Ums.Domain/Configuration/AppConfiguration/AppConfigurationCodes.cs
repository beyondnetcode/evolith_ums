namespace Evolith.Ums.Domain.Configuration.AppConfiguration;

public static class AppConfigurationCodes
{
    public const string SessionTimeoutMinutes = "SESSION_TIMEOUT_MINUTES";
    public const string MaxLoginAttempts = "MAX_LOGIN_ATTEMPTS";
    // ADR-UMS-095: duración (en minutos) del bloqueo temporal por intentos fallidos.
    public const string AccountLockoutDurationMinutes = "ACCOUNT_LOCKOUT_DURATION_MINUTES";
    public const string AccessTokenDurationMs = "ACCESS_TOKEN_DURATION_MS";
    public const string RefreshTokenDurationMs = "REFRESH_TOKEN_DURATION_MS";
    public const string MinPasswordLength = "MIN_PASSWORD_LENGTH";
    public const string MaxValidityPeriodDays = "MAX_VALIDITY_PERIOD_DAYS";
    public const string MfaRequiredForAdmin = "MFA_REQUIRED_FOR_ADMIN";
    public const string MfaAllowedMethods = "MFA_ALLOWED_METHODS";
    public const string UiCustomBrandingEnabled = "UI_CUSTOM_BRANDING_ENABLED";
    public const string FrontendConfigTransport = "FRONTEND_CONFIG_TRANSPORT";
    public const string AuthUseExternalIdp = "AUTH_USE_EXTERNAL_IDP";
    public const string UiLanguageDefault = "UI_LANGUAGE_DEFAULT";
    public const string UiTimezoneDefault = "UI_TIMEZONE_DEFAULT";

    // Refresh token configurable por inquilino + revocación (ADR-UMS-091 / FR-015/016).
    // Resueltos por la cascada jerárquica (Global > Suite > Tenant > Module) con
    // política fail-closed: ausentes o inválidos ⇒ deshabilitado.
    public const string AuthRefreshTokenEnabled = "AUTH_REFRESH_TOKEN_ENABLED";
    public const string AuthRefreshTokenLifetimeMinutes = "AUTH_REFRESH_TOKEN_LIFETIME_MINUTES";
    public const string AuthRefreshTokenRotate = "AUTH_REFRESH_TOKEN_ROTATE";
    public const string AuthRefreshTokenDetectReuse = "AUTH_REFRESH_TOKEN_DETECT_REUSE";
    public const string AuthRefreshTokenMaxRenewals = "AUTH_REFRESH_TOKEN_MAX_RENEWALS";
}
