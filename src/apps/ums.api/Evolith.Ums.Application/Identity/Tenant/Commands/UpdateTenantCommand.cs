namespace Evolith.Ums.Application.Identity.Tenant.Commands;

// FS-26 (G-024): actualiza los datos generales editables del tenant. El Code es inmutable.
public sealed record UpdateTenantCommand(
    Guid TenantId,
    string Name,
    string Type,
    string? CompanyReference) : ICommand;
