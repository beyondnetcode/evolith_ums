using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;



namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;


public sealed record ReactivateBranchCommand(
    Guid TenantId,
    Guid BranchId) : ICommand<ReactivateBranchResponse>;
