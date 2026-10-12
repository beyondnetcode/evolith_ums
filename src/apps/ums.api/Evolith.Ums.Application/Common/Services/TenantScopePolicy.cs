using Evolith.Ums.Domain.Identity;

namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Default tenant scope policy used by portal management flows.
/// It resolves the visible tenant scope and validates management-owner access
/// for operations that can modify UMS configuration.
/// </summary>
public sealed class TenantScopePolicy : ITenantScopePolicy
{
    private readonly ITenantContext _tenantContext;
    private readonly IUserContext _userContext;
    private readonly ITenantRepository _tenantRepository;

    public TenantScopePolicy(
        ITenantContext tenantContext,
        IUserContext userContext,
        ITenantRepository tenantRepository)
    {
        _tenantContext = tenantContext;
        _userContext = userContext;
        _tenantRepository = tenantRepository;
    }

    public Guid? ResolveQueryScope()
    {
        // Internal admins have cross-tenant visibility: no scope filter (null = all tenants).
        // When an internal admin narrows to a specific tenant, the request no longer carries
        // the internal-admin flag, so it falls through to the scoped branches below.
        if (_tenantContext.IsInternalAdmin)
        {
            return null;
        }

        if (_tenantContext.OrganizationId.HasValue)
        {
            return _tenantContext.OrganizationId;
        }

        return Guid.TryParse(_userContext.TenantId, out var tenantId) ? tenantId : null;
    }

    public async Task<Result> EnsureManagementOwnerScopeAsync(Guid targetTenantId, CancellationToken cancellationToken = default)
    {
        var currentTenantId = ResolveCurrentTenantId();
        if (currentTenantId is null)
        {
            return Result.Failure("AUTH_013: Tenant context is required for management access.");
        }

        // ADR-0077 aclarado (evolith_arch#18): el flag IsManagementOwner autoriza al OPERADOR
        // que actúa, no al inquilino OBJETIVO de la acción. Un internal-admin (operador de
        // gestión) puede ejecutar comandos mutantes sobre cualquier inquilino gestionado
        // (on-behalf). Se re-apunta el scope del request al inquilino objetivo para que las
        // lecturas de repositorio dentro de este comando resuelvan los datos del objetivo
        // (los usuarios/roles cliente están filtrados por OrganizationId y no hacen bypass
        // para el internal-admin). La escritura queda auditada por el aspecto [AuditTrail].
        if (_tenantContext.IsInternalAdmin)
        {
            var targetTenant = await _tenantRepository.GetByIdAsync(targetTenantId, cancellationToken);
            if (targetTenant is null)
            {
                return Result.Failure("AUTH_002: Tenant not found.");
            }

            _tenantContext.SetOrganizationId(targetTenantId);
            return Result.Success();
        }

        if (currentTenantId.Value != targetTenantId)
        {
            return Result.Failure(
                $"AUTH_014: Tenant mismatch. User belongs to tenant '{currentTenantId}', but request targets '{targetTenantId}'.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(targetTenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure("AUTH_002: Tenant not found.");
        }

        if (!tenant.IsManagementOwner)
        {
            return Result.Failure("AUTH_015: Tenant is not marked as management owner.");
        }

        return Result.Success();
    }

    private Guid? ResolveCurrentTenantId()
    {
        if (_tenantContext.OrganizationId.HasValue)
        {
            return _tenantContext.OrganizationId;
        }

        return Guid.TryParse(_userContext.TenantId, out var tenantId) ? tenantId : null;
    }
}
