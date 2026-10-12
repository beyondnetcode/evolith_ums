using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Queries;

public sealed record GetNotificationRuleByIdQuery(Guid NotificationRuleId) : IQuery<NotificationRuleDto>;
