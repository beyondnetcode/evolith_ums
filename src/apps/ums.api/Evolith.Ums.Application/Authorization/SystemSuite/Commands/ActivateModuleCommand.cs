namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record ActivateModuleCommand(Guid SystemSuiteId, Guid ModuleId) : ICommand;
