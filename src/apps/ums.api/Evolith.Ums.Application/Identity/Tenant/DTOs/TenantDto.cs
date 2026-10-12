namespace Evolith.Ums.Application.Identity.Tenant.DTOs;

public sealed record TenantDto(
    Guid TenantId,
    string Code,
    string Name,
    string Type,
    string Status,
    Guid? ParentTenantId,
    string? CompanyReference,
    bool IsManagementOwner);
