namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record RenameActionCommand(Guid SystemSuiteId, string Code, string Name) : ICommand;
