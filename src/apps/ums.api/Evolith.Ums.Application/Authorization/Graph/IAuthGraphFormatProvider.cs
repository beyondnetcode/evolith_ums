namespace Evolith.Ums.Application.Authorization.Graph;

/// <summary>
/// Resolves the graph serialization format for a tenant from the parameter catalog.
/// Reads AUTH_GRAPH_DEFAULT_FORMAT and AUTH_GRAPH_ALLOWED_FORMATS via
/// ITenantParameterProvider — same pattern as TenantExportConfigurationProvider.
/// </summary>
public interface IAuthGraphFormatProvider
{
    /// <summary>Returns the tenant's configured default format, e.g. "JSON".</summary>
    Task<string> GetDefaultFormatAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Returns the list of allowed formats for the tenant, e.g. ["JSON","XML","YAML","CSV"].</summary>
    Task<IReadOnlyList<string>> GetAllowedFormatsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a requested format against the tenant's allowed list.
    /// Falls back to the default format if the requested format is not allowed.
    /// </summary>
    Task<string> ResolveFormatAsync(Guid tenantId, string? requestedFormat, CancellationToken cancellationToken = default);
}
