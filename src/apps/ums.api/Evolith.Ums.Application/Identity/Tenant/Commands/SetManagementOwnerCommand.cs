namespace Evolith.Ums.Application.Identity.Tenant.Commands;

public sealed record SetManagementOwnerCommand(Guid TenantId, bool Value) : ICommand;
