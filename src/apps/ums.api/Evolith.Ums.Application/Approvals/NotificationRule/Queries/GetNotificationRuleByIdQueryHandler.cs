using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.NotificationRule;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Queries;

public sealed class GetNotificationRuleByIdQueryHandler : IQueryHandler<GetNotificationRuleByIdQuery, NotificationRuleDto>
{
    private readonly INotificationRuleRepository _repository;

    public GetNotificationRuleByIdQueryHandler(INotificationRuleRepository repository) => _repository = repository;

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approval_rules", ActionCode = "view")]
    public async Task<Result<NotificationRuleDto>> Handle(GetNotificationRuleByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.NotificationRuleId, cancellationToken);
        if (entity is null) return Result<NotificationRuleDto>.Failure("Notification rule not found.");

        return Result<NotificationRuleDto>.Success(new NotificationRuleDto(
            entity.Props.Id.GetValue(), entity.Props.TenantId.GetValue(), entity.Props.Channel.ToString(),
            entity.Props.Recipient.GetValue(), entity.Props.IsActive));
    }
}
