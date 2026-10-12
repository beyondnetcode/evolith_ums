namespace Evolith.Ums.Application.Authorization.Template.DTOs;

public sealed record PermissionTemplateDetailDto(
    Guid TemplateId,
    Guid TenantId,
    Guid RoleId,
    string RoleName,
    Guid SystemSuiteId,
    string SystemSuiteName,
    string Version,
    string Status,
    IReadOnlyList<PermissionTemplateItemDto> Items);
