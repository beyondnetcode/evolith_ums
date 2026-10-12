namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record RemoveAppSettingCommand(Guid SystemSuiteId, string Key) : ICommand;
