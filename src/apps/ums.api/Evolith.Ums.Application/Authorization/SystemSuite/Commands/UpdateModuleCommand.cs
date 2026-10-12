namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record UpdateModuleCommand(
    Guid SystemSuiteId,
    Guid ModuleId,
    string Name,
    string Description,
    int SortOrder) : ICommand;
