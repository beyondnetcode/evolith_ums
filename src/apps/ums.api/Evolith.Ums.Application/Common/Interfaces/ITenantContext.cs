namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Domain-agnostic port to access the current tenant/organization context.
/// Implemented by the Infrastructure layer via Scoped lifecycle.
///
/// Supports two access modes:
/// - Regular tenant access: Users can only see their own tenant's data
/// - Internal Admin access: Users with IsInternalAdmin=true can access multiple tenants
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Current organization/tenant ID. Null means cross-tenant access (internal admin).
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// Original tenant ID of the logged-in user. Used when switching tenant contexts as admin.
    /// </summary>
    Guid? OriginalTenantId { get; }

    /// <summary>
    /// Indicates if the current user is an internal admin with cross-tenant access.
    /// </summary>
    bool IsInternalAdmin { get; }

    /// <summary>
    /// Sets the organization context for the current request.
    /// For regular users, this restricts access to their own tenant.
    /// For internal admins, this can be changed to access different tenants.
    /// </summary>
    void SetOrganizationId(Guid organizationId);

    /// <summary>
    /// Enables cross-tenant access for internal administrators.
    /// When enabled, OrganizationId is set to null and all tenant data is visible.
    /// </summary>
    void EnableCrossTenantAccess();

    /// <summary>
    /// Disables cross-tenant access and restricts to a specific tenant.
    /// For internal admins, use this to switch to a specific tenant context.
    /// </summary>
    void DisableCrossTenantAccess();

    /// <summary>
    /// Initializes the context with the user's original tenant and admin status.
    /// Should be called during authentication.
    /// </summary>
    void Initialize(Guid userTenantId, bool isInternalAdmin);
}