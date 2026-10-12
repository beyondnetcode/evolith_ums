namespace Evolith.Ums.Application.Configuration.AppConfiguration.Queries;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;
using Evolith.Ums.Application.Configuration.Services;

public sealed class ResolveAppConfigurationQueryHandler
    : IQueryHandler<ResolveAppConfigurationQuery, ResolvedAppConfigurationDto>
{
    private readonly IConfigurationProvider  _configProvider;
    private readonly ITenantContext          _tenantContext;
    private readonly IValueEncryptionService _encryption;

    public ResolveAppConfigurationQueryHandler(
        IConfigurationProvider  configProvider,
        ITenantContext          tenantContext,
        IValueEncryptionService encryption)
    {
        _configProvider = configProvider;
        _tenantContext  = tenantContext;
        _encryption     = encryption;
    }

    [AuthorizationAspect(ResourceCode = "view_params", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public Task<Result<ResolvedAppConfigurationDto>> Handle(
        ResolveAppConfigurationQuery request,
        CancellationToken            cancellationToken)
    {
        var config = _configProvider.GetWithPrecedence(
            request.Code,
            request.TenantId,
            request.SuiteId,
            request.ModuleId);

        if (config is null)
        {
            return Task.FromResult(Result<ResolvedAppConfigurationDto>.Success(
                new ResolvedAppConfigurationDto(
                    request.Code, string.Empty, "None", null, false, Found: false)));
        }

        var storedValue  = config.Props.Value.GetValue();
        var isEncrypted  = config.Props.IsEncrypted;
        var displayValue = ResolveDisplay(storedValue, isEncrypted);

        return Task.FromResult(Result<ResolvedAppConfigurationDto>.Success(
            new ResolvedAppConfigurationDto(
                config.Props.Code.GetValue(),
                displayValue,
                config.Props.Scope.Name,
                config.Props.Id.GetValue(),
                isEncrypted,
                Found: true)));
    }

    private string ResolveDisplay(string storedValue, bool isEncrypted)
    {
        if (!isEncrypted) return storedValue;
        if (!_tenantContext.IsInternalAdmin) return "***";

        // The cache already holds decrypted plaintext (ConfigurationProvider.DecryptIfNeeded),
        // but if called outside cache context the value may still carry the AES256: prefix.
        return _encryption.IsEncryptedValue(storedValue)
            ? _encryption.Decrypt(storedValue)
            : storedValue;
    }
}
