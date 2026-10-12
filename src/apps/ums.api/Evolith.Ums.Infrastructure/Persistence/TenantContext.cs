namespace Evolith.Ums.Infrastructure.Persistence;

/// <summary>
/// Concrete implementation of the tenant container.
/// Registered with Scoped lifecycle in the DI container.
///
/// Supports two access modes:
/// - Regular tenant access: Users can only see their own tenant's data
/// - Internal Admin access: Users with IsInternalAdmin=true can access multiple tenants
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid? OrganizationId { get; private set; }
    public Guid? OriginalTenantId { get; private set; }
    public bool IsInternalAdmin { get; private set; }

    private bool _isInitialized;
    private bool _crossTenantAccessEnabled;

    public void Initialize(Guid userTenantId, bool isInternalAdmin)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException("Tenant context has already been initialized for this request.");
        }

        OriginalTenantId = userTenantId;
        IsInternalAdmin = isInternalAdmin;
        OrganizationId = userTenantId; // Default to user's own tenant
        _isInitialized = true;
        _crossTenantAccessEnabled = false;
    }

    public void SetOrganizationId(Guid organizationId)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Tenant context must be initialized before setting OrganizationId.");
        }

        if (IsInternalAdmin && _crossTenantAccessEnabled)
        {
            // Internal admins with cross-tenant access enabled can switch contexts
            OrganizationId = organizationId;
        }
        else if (IsInternalAdmin && !_crossTenantAccessEnabled)
        {
            // Internal admin but cross-tenant not explicitly enabled - allow switch
            OrganizationId = organizationId;
        }
        else
        {
            // Regular users cannot change their tenant context
            if (organizationId != OriginalTenantId)
            {
                throw new InvalidOperationException(
                    "Regular users cannot access other tenants. " +
                    $"Expected: {OriginalTenantId}, Received: {organizationId}");
            }
            OrganizationId = organizationId;
        }
    }

    public void EnableCrossTenantAccess()
    {
        if (!IsInternalAdmin)
        {
            throw new InvalidOperationException("Only internal administrators can enable cross-tenant access.");
        }

        _crossTenantAccessEnabled = true;
        OrganizationId = null; // null means all tenant data is visible
    }

    public void DisableCrossTenantAccess()
    {
        _crossTenantAccessEnabled = false;
        OrganizationId = OriginalTenantId; // Reset to user's original tenant
    }

    public void Reset()
    {
        OrganizationId = OriginalTenantId;
        _crossTenantAccessEnabled = false;
    }
}