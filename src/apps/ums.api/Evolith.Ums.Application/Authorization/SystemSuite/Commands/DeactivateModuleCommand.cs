namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record DeactivateModuleCommand(Guid SystemSuiteId, Guid ModuleId) : ICommand;
