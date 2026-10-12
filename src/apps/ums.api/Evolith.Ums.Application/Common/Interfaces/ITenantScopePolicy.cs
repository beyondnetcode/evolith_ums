namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Resolves tenant-scoped access rules for portal management operations.
/// The policy keeps tenant ownership checks and management-owner validation
/// in one place so handlers remain small and predictable.
/// </summary>
public interface ITenantScopePolicy
{
    /// <summary>
    /// Resolves the tenant scope visible to the current request.
    /// Internal admins can optionally operate cross-tenant; regular users are
    /// always restricted to their own tenant.
    /// </summary>
    Guid? ResolveQueryScope();

    /// <summary>
    /// Validates that the current request targets the caller's own tenant and
    /// that the tenant is marked as management owner.
    /// </summary>
    Task<Result> EnsureManagementOwnerScopeAsync(Guid targetTenantId, CancellationToken cancellationToken = default);
}
