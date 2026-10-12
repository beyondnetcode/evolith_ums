namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record RemoveModuleCommand(Guid SystemSuiteId, Guid ModuleId) : ICommand;
