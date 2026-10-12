using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;
using Evolith.Ums.Domain.Identity.Tenant;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Queries;

public sealed class GetBranchesByTenantIdQueryHandler : IQueryHandler<GetBranchesByTenantIdQuery, IReadOnlyList<BranchDto>>
{
    private readonly ITenantRepository _tenantRepository;

    public GetBranchesByTenantIdQueryHandler(ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_tenants", ActionCode = "view")]
    public async Task<Result<IReadOnlyList<BranchDto>>> Handle(
        GetBranchesByTenantIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result<IReadOnlyList<BranchDto>>.Failure("Tenant not found.");
        }

        // ADR-0164: el filtro de las cerradas vive AQUÍ, en la lectura, y no en un filtro global de
        // EF. Con un filtro global la sucursal cerrada desaparecería también de la vía de escritura y
        // de la resolución por id —el grafo de autorización resuelve la sucursal de un perfil por su
        // id—, y el borrado lógico habría acabado siendo tan opaco como el físico que sustituye.
        var branches = tenant.Branches
            .Where(b => request.IncludeClosed || !b.IsClosed)
            .Select(b => new BranchDto(
                b.GetId().GetValue(),
                b.Code.GetValue(),
                b.Name.GetValue(),
                b.IsActive,
                b.GeofencingMetadata?.GetValue(),
                b.IsClosed,
                b.ClosedAtUtc))
            .ToList();

        return Result<IReadOnlyList<BranchDto>>.Success(branches.AsReadOnly());
    }
}
