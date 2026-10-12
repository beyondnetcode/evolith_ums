namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record UpdateAppSettingCommand(Guid SystemSuiteId, string Key, string NewValue) : ICommand;
