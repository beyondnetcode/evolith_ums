using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;

public sealed record CreateNotificationRuleCommand(
    Guid TenantId, string Channel, string Recipient) : ICommand<CreateNotificationRuleResponse>;
