using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Branch.Queries;

/// <summary>
/// Bitácora de una sucursal: sus episodios en orden cronológico (ADR-0164).
/// Responde a la pregunta que el estado no puede responder: «¿cómo estaba esta sucursal cuando salió
/// aquel despacho, y quién decidió cada cambio?».
/// </summary>
public sealed record GetBranchLifecycleQuery(
    Guid TenantId,
    Guid BranchId) : IQuery<IReadOnlyList<BranchLifecycleEntryDto>>;
