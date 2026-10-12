namespace Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

/// <summary>
/// Un asiento de la bitácora de una sucursal, tal y como sale por el API (ADR-0164).
/// El episodio viaja como NOMBRE ("Opened", "Deactivated", "Reactivated", "Closed") y no como
/// número: quien lee una auditoría no debería tener que consultar una tabla de equivalencias.
/// </summary>
public sealed record BranchLifecycleEntryDto(
    Guid EntryId,
    string Episode,
    DateTime OccurredAtUtc,
    string ActorId,
    string NameSnapshot,
    string? GeofencingSnapshot,
    string? Reason);
