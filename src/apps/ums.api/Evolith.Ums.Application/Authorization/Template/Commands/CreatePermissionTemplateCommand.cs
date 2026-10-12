using Evolith.Ums.Application.Authorization.Template.DTOs;

namespace Evolith.Ums.Application.Authorization.Template.Commands;

public sealed record CreatePermissionTemplateCommand(
    Guid TenantId,
    Guid RoleId,
    Guid SystemSuiteId) : ICommand<CreatePermissionTemplateResponse>;
