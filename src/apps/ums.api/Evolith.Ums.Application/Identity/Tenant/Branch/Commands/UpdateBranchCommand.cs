namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;

// FS-26 (G-024): actualiza los datos editables de una sucursal. El Code es inmutable.
public sealed record UpdateBranchCommand(
    Guid TenantId,
    Guid BranchId,
    string Name,
    string? GeofencingMetadata) : ICommand;
