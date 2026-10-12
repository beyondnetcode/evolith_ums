namespace Evolith.Ums.Application.Authorization.Template.DTOs;

public sealed record PermissionTemplateDto(
    Guid TemplateId,
    Guid TenantId,
    Guid RoleId,
    string RoleName,
    Guid SystemSuiteId,
    string SystemSuiteName,
    string Version,
    string Status);
