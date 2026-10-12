using Evolith.Ums.Application.Identity.Tenant.DTOs;


namespace Evolith.Ums.Application.Identity.Tenant.Commands;


public sealed record ActivateTenantCommand(Guid TenantId) : ICommand;
