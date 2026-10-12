namespace Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

public static class TenantParameterCodes
{
    public const string ExportProfilePermissionGraphAllowedFormats = "EXPORT_PROFILE_PERMISSION_GRAPH_ALLOWED_FORMATS";
    public const string ExportProfilePermissionGraphDefaultFormat = "EXPORT_PROFILE_PERMISSION_GRAPH_DEFAULT_FORMAT";
    public const string ExportProfilePermissionGraphIncludeTechnicalMetadata = "EXPORT_PROFILE_PERMISSION_GRAPH_INCLUDE_TECHNICAL_METADATA";
    public const string ExportProfilePermissionGraphMaskGuids = "EXPORT_PROFILE_PERMISSION_GRAPH_MASK_GUIDS";
    public const string ExportProfilePermissionGraphIncludeFeatureFlags = "EXPORT_PROFILE_PERMISSION_GRAPH_INCLUDE_FEATURE_FLAGS";
    public const string ExportProfilePermissionGraphIncludeEffectivePermissionsSummary = "EXPORT_PROFILE_PERMISSION_GRAPH_INCLUDE_EFFECTIVE_PERMISSIONS_SUMMARY";
    public const string ExportProfilePermissionGraphMaxItems = "EXPORT_PROFILE_PERMISSION_GRAPH_MAX_ITEMS";

    // ── Authorization Graph serialization format ─────────────────────────────

    /// <summary>Default serialization format for the authorization graph returned by /client/authenticate.
    /// Supported values: JSON | XML | YAML | CSV. Defaults to JSON.</summary>
    public const string AuthGraphDefaultFormat = "AUTH_GRAPH_DEFAULT_FORMAT";

    /// <summary>Comma-separated list of formats the tenant allows for the auth graph response.
    /// Defaults to ["JSON","XML","YAML","CSV"].</summary>
    public const string AuthGraphAllowedFormats = "AUTH_GRAPH_ALLOWED_FORMATS";

    /// <summary>Whether to include technical metadata (IDs, timestamps) in the serialized graph.
    /// Defaults to false.</summary>
    public const string AuthGraphIncludeTechnicalMetadata = "AUTH_GRAPH_INCLUDE_TECHNICAL_METADATA";
}
