
namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;

public sealed class ArchiveAppConfigurationCommandHandler : ICommandHandler<ArchiveAppConfigurationCommand>
{
    private readonly IAppConfigurationRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IConfigurationProvider _configProvider;

    public ArchiveAppConfigurationCommandHandler(
        IAppConfigurationRepository repository,
        IUserContext userContext,
        IConfigurationProvider configProvider)
    {
        _repository = repository;
        _userContext = userContext;
        _configProvider = configProvider;
    }

    [AuthorizationAspect(ResourceCode = "manage_params", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ArchiveAppConfigurationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required.");
        }

        var appConfiguration = await _repository.GetByIdAsync(request.AppConfigurationId, cancellationToken);
        if (appConfiguration is null)
        {
            return Result.Failure("App configuration was not found.");
        }

        var result = appConfiguration.Archive(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(appConfiguration, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Archiving removes the value from the active configuration — evict from cache.
        var tenantId = appConfiguration.Props.TenantId?.GetValue();
        if (tenantId.HasValue)
            await _configProvider.ReloadTenantAsync(tenantId.Value, cancellationToken);
        else
            await _configProvider.ReloadAsync(cancellationToken);

        return Result.Success();
    }
}
