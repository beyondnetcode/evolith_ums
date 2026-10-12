using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;

namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;

public sealed record DeactivateNotificationRuleCommand(Guid NotificationRuleId) : ICommand;
