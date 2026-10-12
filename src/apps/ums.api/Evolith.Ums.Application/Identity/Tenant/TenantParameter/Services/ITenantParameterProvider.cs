namespace Evolith.Ums.Application.Identity.Tenant.TenantParameter.Services;

/// <summary>
/// Provides access to tenant configuration parameters with automatic tenant isolation.
/// All methods enforce tenant-based data access boundaries.
/// </summary>
public interface ITenantParameterProvider
{
    /// <summary>
    /// Retrieves all parameters for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Read-only list of all tenant parameters.</returns>
    Task<IReadOnlyList<TenantParameterDto>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves only active parameters for a specific tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Read-only list of active tenant parameters.</returns>
    Task<IReadOnlyList<TenantParameterDto>> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific parameter by its code.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="code">The unique code of the parameter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The parameter DTO or null if not found.</returns>
    /// <exception cref="TenantParameterIsolationException">Thrown if parameter belongs to different tenant.</exception>
    Task<TenantParameterDto?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw string value of a parameter.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="code">The unique code of the parameter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The parameter value or null if not found.</returns>
    Task<string?> GetValueAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the value as a boolean with type validation.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="code">The unique code of the parameter.</param>
    /// <param name="defaultValue">Default value if parameter not found or wrong type.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The boolean value or defaultValue.</returns>
    Task<bool> GetBoolValueAsync(Guid tenantId, string code, bool defaultValue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the value as an integer with type validation.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="code">The unique code of the parameter.</param>
    /// <param name="defaultValue">Default value if parameter not found or wrong type.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The integer value or defaultValue.</returns>
    Task<int> GetIntValueAsync(Guid tenantId, string code, int defaultValue = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the value as a string array (comma-separated) with type validation.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <param name="code">The unique code of the parameter.</param>
    /// <param name="defaultValue">Default value if parameter not found or wrong type.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The string array or defaultValue.</returns>
    Task<string[]> GetStringListValueAsync(Guid tenantId, string code, string[] defaultValue = null!, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data transfer object for tenant parameter information.
/// </summary>
/// <param name="Id">Unique identifier of the parameter.</param>
/// <param name="TenantId">The tenant that owns this parameter.</param>
/// <param name="Code">Unique code within the tenant.</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="Value">The current value.</param>
/// <param name="ValueType">Type name (String, Integer, Boolean, StringList, Json).</param>
/// <param name="Category">Category name (Export, Security, Ui, etc.).</param>
/// <param name="IsActive">Whether the parameter is active.</param>
/// <param name="IsSensitive">Whether the value contains sensitive data.</param>
/// <param name="DefaultValue">Default value if not set.</param>
/// <param name="AllowedValues">Comma-separated list of allowed values.</param>
public sealed record TenantParameterDto(
    Guid Id,
    Guid TenantId,
    string Code,
    string Description,
    string Value,
    string ValueType,
    string Category,
    bool IsActive,
    bool IsSensitive,
    string? DefaultValue,
    string? AllowedValues);