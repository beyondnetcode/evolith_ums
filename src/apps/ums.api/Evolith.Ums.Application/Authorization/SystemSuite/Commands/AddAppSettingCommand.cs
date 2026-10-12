namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record AddAppSettingCommand(
    Guid SystemSuiteId,
    string Key,
    string Value,
    string Scope) : ICommand;
