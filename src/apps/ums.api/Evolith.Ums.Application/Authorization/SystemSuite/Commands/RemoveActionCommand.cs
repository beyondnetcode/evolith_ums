namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record RemoveActionCommand(Guid SystemSuiteId, string Code) : ICommand;
