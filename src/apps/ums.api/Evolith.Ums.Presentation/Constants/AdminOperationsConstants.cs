namespace Evolith.Ums.Presentation.Constants;

using Evolith.Ums.Domain.Configuration.AppConfiguration;

public static class FeatureFlagKeys
{
    public const string AllowPasswordResetByAdmin = "ALLOW_PASSWORD_RESET_BY_ADMIN";
    public const string AllowValidityPeriodModification = "ALLOW_VALIDITY_PERIOD_MODIFICATION";
}

public static class AuditEventTypes
{
    public const string PasswordReset = "PASSWORD_RESET";
    public const string ValidityPeriodModified = "VALIDITY_PERIOD_MODIFIED";
}

public static class PermissionCodes
{
    public const string CanResetPassword = "CAN_RESET_PASSWORD";
    public const string CanModifyValidityPeriod = "CAN_MODIFY_VALIDITY_PERIOD";
}

public static class AppConfigKeys
{
    public const string MaxValidityPeriodDays = AppConfigurationCodes.MaxValidityPeriodDays;
    public const string MinPasswordLength = AppConfigurationCodes.MinPasswordLength;
    public const string PasswordResetNotificationChannel = "PASSWORD_RESET_NOTIFICATION_CHANNEL";
}
