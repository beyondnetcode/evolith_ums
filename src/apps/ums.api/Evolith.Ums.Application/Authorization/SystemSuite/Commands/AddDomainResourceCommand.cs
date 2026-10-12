using System;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;

public sealed record AddDomainResourceCommand(
    Guid SystemSuiteId,
    Guid? ModuleId,
    Guid? ParentResourceId,
    string Type,
    string Code,
    string Name,
    string Description) : ICommand<AddDomainResourceResponse>;
