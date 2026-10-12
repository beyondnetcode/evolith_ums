using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Commands;

using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Identity;

public sealed class RejectRequestCommandHandler : ICommandHandler<RejectRequestCommand>
{
    private readonly IApprovalRequestRepository _repository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly INotificationService _notificationService;
    private readonly IUserContext _userContext;

    public RejectRequestCommandHandler(
        IApprovalRequestRepository repository,
        IUserAccountRepository userAccountRepository,
        ITenantRepository tenantRepository,
        INotificationService notificationService,
        IUserContext userContext)
    {
        _repository = repository;
        _userAccountRepository = userAccountRepository;
        _tenantRepository = tenantRepository;
        _notificationService = notificationService;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approvals", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(RejectRequestCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var entity = await _repository.GetByIdAsync(request.ApprovalRequestId, cancellationToken);
        if (entity is null) return Result.Failure("Approval request not found.");

        var result = entity.Reject(ActorId.Create(_userContext.UserId), request.DecisionReason);
        if (result.IsFailure) return result;

        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var targetUser = await _userAccountRepository.GetByIdAsync(entity.TargetUserId.GetValue(), cancellationToken);
        if (targetUser is not null)
        {
            var tenant = await _tenantRepository.GetByIdAsync(targetUser.TenantId.GetValue(), cancellationToken);
            await _notificationService.SendAsync(
                NotificationTemplates.ProfileRequestDenied(
                    targetUser.Email.GetValue(),
                    targetUser.DisplayName?.GetValue() ?? targetUser.Email.GetValue(),
                    tenant?.Name.GetValue() ?? "your organization",
                    entity.RequestedSystemId.GetValue().ToString(),
                    request.DecisionReason),
                cancellationToken);
        }

        return Result.Success();
    }
}
