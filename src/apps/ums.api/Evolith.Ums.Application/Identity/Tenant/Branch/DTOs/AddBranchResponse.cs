namespace Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

public sealed record AddBranchResponse(Guid TenantId, Guid BranchId, string Code);
