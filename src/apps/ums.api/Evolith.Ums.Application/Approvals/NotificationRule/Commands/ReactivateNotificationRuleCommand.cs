namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;

public sealed record ReactivateNotificationRuleCommand(Guid NotificationRuleId) : ICommand;
