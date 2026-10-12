using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record CreateSystemSuiteCommand(
    Guid TenantId,
    string Code,
    string Name,
    string Description) : ICommand<CreateSystemSuiteResponse>;
