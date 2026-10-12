namespace Evolith.Ums.Application.Authorization.Profile.Exporters;

/// <summary>
/// Provides tenant-specific export configuration derived from TenantParameters.
/// </summary>
public interface ITenantExportConfigurationProvider
{
    /// <summary>
    /// Gets the export configuration for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The export configuration for the tenant.</returns>
    Task<ExportConfiguration> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Configuration options for profile permission graph exports.
/// </summary>
/// <param name="AllowedFormats">List of allowed export formats (JSON, XML, YAML, CSV).</param>
/// <param name="DefaultFormat">Default format when not specified.</param>
/// <param name="IncludeTechnicalMetadata">Whether to include tenant, system, and session metadata.</param>
/// <param name="MaskGuids">Whether to mask GUIDs in the output for privacy.</param>
/// <param name="IncludeFeatureFlags">Whether to include feature flag information.</param>
/// <param name="IncludeEffectivePermissionsSummary">Whether to include permission summary statistics.</param>
/// <param name="MaxItems">Maximum number of items to include in the export.</param>
public sealed record ExportConfiguration(
    IReadOnlyList<string> AllowedFormats,
    string DefaultFormat,
    bool IncludeTechnicalMetadata,
    bool MaskGuids,
    bool IncludeFeatureFlags,
    bool IncludeEffectivePermissionsSummary,
    int MaxItems)
{
    /// <summary>
    /// Default configuration with standard settings.
    /// </summary>
    public static ExportConfiguration Default => new(
        ["JSON", "XML", "YAML", "CSV"],
        "JSON",
        true,
        false,
        true,
        true,
        10000);
}