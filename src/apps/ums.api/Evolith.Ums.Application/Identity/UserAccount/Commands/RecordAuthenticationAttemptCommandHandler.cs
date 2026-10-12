namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Identity;

public sealed class RecordAuthenticationAttemptCommandHandler
    : ICommandHandler<RecordAuthenticationAttemptCommand>
{
    private readonly IUserAccountRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IConfigurationProvider _configurationProvider;

    public RecordAuthenticationAttemptCommandHandler(
        IUserAccountRepository repository,
        IUserContext userContext,
        IConfigurationProvider configurationProvider)
    {
        _repository = repository;
        _userContext = userContext;
        _configurationProvider = configurationProvider;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(
        RecordAuthenticationAttemptCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var entity = await _repository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (entity is null) return Result.Failure("User account not found.");

        var actor = ActorId.Create(_userContext.UserId);
        // ADR-UMS-095: el instante y los parámetros de política se resuelven en la aplicación y se
        // inyectan en el dominio determinista (cascada de config Global>Suite>Tenant>Module).
        var cfg = _configurationProvider.ForTenant(entity.Props.TenantId.GetValue());
        var result = entity.RecordAuthenticationAttempt(
            request.Success,
            DateTimeOffset.UtcNow,
            cfg.MaxLoginAttempts,
            cfg.AccountLockoutDurationMinutes,
            request.Reason,
            request.IpAddress,
            actor);

        if (result.IsFailure) return result;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
