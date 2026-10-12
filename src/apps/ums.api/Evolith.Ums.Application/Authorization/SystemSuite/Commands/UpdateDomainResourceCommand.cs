using System;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed record UpdateDomainResourceCommand(
    Guid SystemSuiteId,
    Guid DomainResourceId,
    string Name,
    string Description) : ICommand;
