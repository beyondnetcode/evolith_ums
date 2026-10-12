using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record UpdateSystemSuiteCommand(
    Guid SystemSuiteId,
    string Name,
    string Description) : ICommand;
