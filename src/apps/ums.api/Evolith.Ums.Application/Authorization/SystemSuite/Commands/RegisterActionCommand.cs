namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

public sealed record RegisterActionCommand(Guid SystemSuiteId, string Code, string Name) : ICommand<RegisterActionResponse>;
