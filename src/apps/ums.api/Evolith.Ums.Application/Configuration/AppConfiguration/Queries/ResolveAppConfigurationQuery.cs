namespace Evolith.Ums.Application.Configuration.AppConfiguration.Queries;

using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

public sealed record ResolveAppConfigurationQuery(
    string Code,
    Guid?  TenantId,
    Guid?  SuiteId,
    Guid?  ModuleId) : IQuery<ResolvedAppConfigurationDto>;
