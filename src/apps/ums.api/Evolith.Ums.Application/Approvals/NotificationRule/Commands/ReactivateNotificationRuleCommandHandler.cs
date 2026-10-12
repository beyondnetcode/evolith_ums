namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;

using Evolith.Ums.Domain.Approvals;

public sealed class ReactivateNotificationRuleCommandHandler : ICommandHandler<ReactivateNotificationRuleCommand>
{
    private readonly INotificationRuleRepository _repository;
    private readonly IUserContext _userContext;

    public ReactivateNotificationRuleCommandHandler(
        INotificationRuleRepository repository,
        IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approval_rules", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ReactivateNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to reactivate a notification rule.");
        }

        var entity = await _repository.GetByIdAsync(request.NotificationRuleId, cancellationToken);
        if (entity is null)
        {
            return Result.Failure("Notification rule not found.");
        }

        var result = entity.Reactivate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
