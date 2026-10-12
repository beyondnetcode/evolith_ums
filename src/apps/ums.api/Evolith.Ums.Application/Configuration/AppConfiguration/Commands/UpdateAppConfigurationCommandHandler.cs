
namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;

public sealed class UpdateAppConfigurationCommandHandler : ICommandHandler<UpdateAppConfigurationCommand>
{
    private readonly IAppConfigurationRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IConfigurationProvider _configProvider;
    private readonly IValueEncryptionService _encryption;

    public UpdateAppConfigurationCommandHandler(
        IAppConfigurationRepository repository,
        IUserContext userContext,
        IConfigurationProvider configProvider,
        IValueEncryptionService encryption)
    {
        _repository = repository;
        _userContext = userContext;
        _configProvider = configProvider;
        _encryption = encryption;
    }

    [AuthorizationAspect(ResourceCode = "manage_params", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(UpdateAppConfigurationCommand request, CancellationToken cancellationToken)
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

        var storedValue = appConfiguration.Props.IsEncrypted
            ? _encryption.Encrypt(request.Value)
            : request.Value;

        var result = appConfiguration.Update(
            ConfigurationValue.Create(storedValue),
            Description.Create(request.Description),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return result;
        }

        // REC-10: Pass RowVersion from If-Match header to enforce ETag-based optimistic locking.
        await _repository.UpdateAsync(appConfiguration, request.RowVersion, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // Invalidate the in-memory cache so the updated value is reflected immediately.
        // For tenant-scoped configs reload only that tenant; global configs reload all.
        var tenantId = appConfiguration.Props.TenantId?.GetValue();
        if (tenantId.HasValue)
            await _configProvider.ReloadTenantAsync(tenantId.Value, cancellationToken);
        else
            await _configProvider.ReloadAsync(cancellationToken);

        return Result.Success();
    }
}
