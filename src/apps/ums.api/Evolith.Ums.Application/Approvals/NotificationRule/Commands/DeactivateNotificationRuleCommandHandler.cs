using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;

using Evolith.Ums.Domain.Approvals;

public sealed class DeactivateNotificationRuleCommandHandler : ICommandHandler<DeactivateNotificationRuleCommand>
{
    private readonly INotificationRuleRepository _repository;
    private readonly IUserContext _userContext;

    public DeactivateNotificationRuleCommandHandler(INotificationRuleRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approval_rules", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeactivateNotificationRuleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var entity = await _repository.GetByIdAsync(request.NotificationRuleId, cancellationToken);
        if (entity is null) return Result.Failure("Notification rule not found.");

        var result = entity.Deactivate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure) return result;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
