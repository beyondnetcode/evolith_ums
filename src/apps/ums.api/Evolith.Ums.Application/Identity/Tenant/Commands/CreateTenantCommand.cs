using Evolith.Ums.Application.Identity.Tenant.DTOs;



namespace Evolith.Ums.Application.Identity.Tenant.Commands;


public sealed record CreateTenantCommand(
    string Code,
    string Name,
    string Type,
    string? IdpStrategy,
    string? CompanyReference,
    bool IsManagementOwner = false) : ICommand<CreateTenantResponse>;
