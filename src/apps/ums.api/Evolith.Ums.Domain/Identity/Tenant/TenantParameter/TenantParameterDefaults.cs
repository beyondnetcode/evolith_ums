namespace Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

public static class TenantParameterDefaults
{
    public static readonly string[] ExportProfilePermissionGraphAllowedFormats = ["JSON", "XML", "YAML", "CSV"];
    public const string ExportProfilePermissionGraphDefaultFormat = "JSON";
    public const bool ExportProfilePermissionGraphIncludeTechnicalMetadata = true;
    public const bool ExportProfilePermissionGraphMaskGuids = false;
    public const bool ExportProfilePermissionGraphIncludeFeatureFlags = true;
    public const bool ExportProfilePermissionGraphIncludeEffectivePermissionsSummary = true;
    public const int ExportProfilePermissionGraphMaxItems = 10_000;

    public const string AuthGraphDefaultFormat = "JSON";
    public static readonly string[] AuthGraphAllowedFormats = ["JSON", "XML", "YAML", "CSV"];
    public const bool AuthGraphIncludeTechnicalMetadata = false;
}
