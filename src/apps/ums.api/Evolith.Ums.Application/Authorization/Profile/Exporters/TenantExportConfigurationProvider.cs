using Evolith.Ums.Application.Identity.Tenant.TenantParameter.Services;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

namespace Evolith.Ums.Application.Authorization.Profile.Exporters;

public sealed class TenantExportConfigurationProvider : ITenantExportConfigurationProvider
{
    private readonly ITenantParameterProvider _parameterProvider;

    public TenantExportConfigurationProvider(ITenantParameterProvider parameterProvider)
    {
        _parameterProvider = parameterProvider;
    }

    public async Task<ExportConfiguration> GetConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var allowedFormats = await _parameterProvider.GetStringListValueAsync(
            tenantId,
            TenantParameterCodes.ExportProfilePermissionGraphAllowedFormats,
            TenantParameterDefaults.ExportProfilePermissionGraphAllowedFormats,
            cancellationToken);

        var defaultFormatRaw = await _parameterProvider.GetValueAsync(
            tenantId,
            TenantParameterCodes.ExportProfilePermissionGraphDefaultFormat,
            cancellationToken) ?? TenantParameterDefaults.ExportProfilePermissionGraphDefaultFormat;

        var includeTechnicalMetadata = await _parameterProvider.GetBoolValueAsync(
            tenantId,
            TenantParameterCodes.ExportProfilePermissionGraphIncludeTechnicalMetadata,
            TenantParameterDefaults.ExportProfilePermissionGraphIncludeTechnicalMetadata,
            cancellationToken);

        var maskGuids = await _parameterProvider.GetBoolValueAsync(
            tenantId,
            TenantParameterCodes.ExportProfilePermissionGraphMaskGuids,
            TenantParameterDefaults.ExportProfilePermissionGraphMaskGuids,
            cancellationToken);

        var includeFeatureFlags = await _parameterProvider.GetBoolValueAsync(
            tenantId,
            TenantParameterCodes.ExportProfilePermissionGraphIncludeFeatureFlags,
            TenantParameterDefaults.ExportProfilePermissionGraphIncludeFeatureFlags,
            cancellationToken);

        var includeEffectivePermissionsSummary = await _parameterProvider.GetBoolValueAsync(
            tenantId,
            TenantParameterCodes.ExportProfilePermissionGraphIncludeEffectivePermissionsSummary,
            TenantParameterDefaults.ExportProfilePermissionGraphIncludeEffectivePermissionsSummary,
            cancellationToken);

        var maxItems = await _parameterProvider.GetIntValueAsync(
            tenantId,
            TenantParameterCodes.ExportProfilePermissionGraphMaxItems,
            TenantParameterDefaults.ExportProfilePermissionGraphMaxItems,
            cancellationToken);

        var defaultFormat = defaultFormatRaw.ToUpperInvariant();
        if (!allowedFormats.Any(f => f.Equals(defaultFormat, StringComparison.OrdinalIgnoreCase)))
            defaultFormat = TenantParameterDefaults.ExportProfilePermissionGraphDefaultFormat;

        return new ExportConfiguration(
            allowedFormats,
            defaultFormat,
            includeTechnicalMetadata,
            maskGuids,
            includeFeatureFlags,
            includeEffectivePermissionsSummary,
            maxItems);
    }
}
