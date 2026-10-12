using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;



namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;


public sealed record DeactivateBranchCommand(
    Guid TenantId,
    Guid BranchId) : ICommand<DeactivateBranchResponse>;
