using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Queries;

/// <param name="IncludeClosed">
/// ADR-0164: por defecto el listado NO trae las sucursales cerradas definitivamente — para quien
/// opera hoy ya no existen. Se puede pedir que las traiga para revisar el histórico, que es la
/// diferencia entre «oculto» y «borrado»: lo borrado no se puede pedir.
/// </param>
public sealed record GetBranchesByTenantIdQuery(
    Guid TenantId,
    bool IncludeClosed = false) : IQuery<IReadOnlyList<BranchDto>>;
