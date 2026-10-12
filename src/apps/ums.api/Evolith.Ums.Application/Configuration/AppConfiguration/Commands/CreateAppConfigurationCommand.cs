using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

public sealed record CreateAppConfigurationCommand(
    Guid? TenantId,
    Guid? SystemSuiteId,
    Guid? ModuleId,
    string Code,
    string Value,
    string Description,
    bool IsInheritable,
    bool IsEncrypted,
    bool IsNonOverridable = false) : ICommand<CreateAppConfigurationResponse>;
