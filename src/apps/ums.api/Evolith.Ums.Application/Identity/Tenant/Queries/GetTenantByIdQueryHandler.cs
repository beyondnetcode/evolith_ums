using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.DTOs;
using Evolith.Ums.Domain.Identity.Tenant;

namespace Evolith.Ums.Application.Identity.Tenant.Queries;

public sealed class GetTenantByIdQueryHandler : IQueryHandler<GetTenantByIdQuery, TenantDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public GetTenantByIdQueryHandler(ITenantRepository tenantRepository, ITenantScopePolicy tenantScopePolicy)
    {
        _tenantRepository = tenantRepository;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_tenants", ActionCode = "view")]
    public async Task<Result<TenantDto>> Handle(
        GetTenantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result<TenantDto>.Failure("Tenant not found.");
        }

        // TS04/F3 (aislamiento cross-tenant): el agregado Tenant es su propia identidad, así que el
        // global query filter (que aísla entidades con columna TenantId) NO lo cubre. Sin este chequeo,
        // cualquier usuario autenticado leería la identidad de cualquier tenant por id (fuga cross-tenant).
        // ResolveQueryScope() devuelve null para internal-admin (cross-tenant) y el OrganizationId propio
        // para un usuario regular. Se devuelve "not found" (404, no 403) para no filtrar la existencia.
        var scope = _tenantScopePolicy.ResolveQueryScope();
        if (scope.HasValue && tenant.Props.Id.GetValue() != scope.Value)
        {
            return Result<TenantDto>.Failure("Tenant not found.");
        }

        return Result<TenantDto>.Success(new TenantDto(
            tenant.Props.Id.GetValue(),
            tenant.Props.Code.GetValue(),
            tenant.Props.Name.GetValue(),
            tenant.Props.Type.ToString(),
            tenant.Props.Status.ToString(),
            tenant.Props.ParentTenantId?.GetValue(),
            tenant.Props.CompanyReference?.GetValue(),
            tenant.Props.IsManagementOwner));
    }
}
