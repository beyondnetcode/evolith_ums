using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record SetSystemSuiteStatusCommand(Guid SystemSuiteId, string Status) : ICommand;
