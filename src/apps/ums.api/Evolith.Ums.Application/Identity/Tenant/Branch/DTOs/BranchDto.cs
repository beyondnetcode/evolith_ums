namespace Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

/// <param name="IsClosed">
/// Cierre definitivo (ADR-0164). Se expone —y no solo se filtra— porque quien pida explícitamente
/// las cerradas debe poder distinguirlas de las meramente desactivadas: son dos cosas distintas.
/// </param>
public sealed record BranchDto(
    Guid BranchId,
    string Code,
    string Name,
    bool IsActive,
    string? GeofencingMetadata,
    bool IsClosed = false,
    DateTime? ClosedAtUtc = null);
