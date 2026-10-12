using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;



namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;


public sealed record AddBranchCommand(
    Guid TenantId,
    string Code,
    string Name,
    string? GeofencingMetadata) : ICommand<AddBranchResponse>;
