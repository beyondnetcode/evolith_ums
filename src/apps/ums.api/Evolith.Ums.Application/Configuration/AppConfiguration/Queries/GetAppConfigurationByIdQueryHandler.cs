using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.AppConfiguration.Queries;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;

public sealed class GetAppConfigurationByIdQueryHandler : IQueryHandler<GetAppConfigurationByIdQuery, AppConfigurationDto>
{
    private readonly IAppConfigurationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IValueEncryptionService _encryption;

    public GetAppConfigurationByIdQueryHandler(
        IAppConfigurationRepository repository,
        ITenantContext tenantContext,
        IValueEncryptionService encryption)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _encryption = encryption;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_params", ActionCode = "view")]
    public async Task<Result<AppConfigurationDto>> Handle(GetAppConfigurationByIdQuery request, CancellationToken cancellationToken)
    {
        var appConfiguration = await _repository.GetByIdAsync(request.AppConfigurationId, cancellationToken);
        if (appConfiguration is null)
        {
            return Result<AppConfigurationDto>.Failure("App configuration was not found.");
        }

        return Result<AppConfigurationDto>.Success(new AppConfigurationDto(
            appConfiguration.Props.Id.GetValue(),
            appConfiguration.Props.TenantId?.GetValue(),
            appConfiguration.Props.SystemSuiteId?.GetValue(),
            appConfiguration.Props.ModuleId?.GetValue(),
            appConfiguration.Props.Code.GetValue(),
            ResolveValue(appConfiguration.Props.Value.GetValue(), appConfiguration.Props.IsEncrypted),
            appConfiguration.Props.Description.GetValue(),
            appConfiguration.Props.Scope.Name,
            appConfiguration.Props.IsInheritable,
            appConfiguration.Props.IsEncrypted,
            appConfiguration.Props.IsNonOverridable,
            appConfiguration.Props.Version,
            appConfiguration.Props.Status.Name));
    }

    private string ResolveValue(string storedValue, bool isEncrypted)
    {
        if (!isEncrypted) return storedValue;
        if (!_tenantContext.IsInternalAdmin) return "***";
        return _encryption.IsEncryptedValue(storedValue) ? _encryption.Decrypt(storedValue) : storedValue;
    }
}
