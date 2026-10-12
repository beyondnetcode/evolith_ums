namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;

public sealed record UpdateNotificationRuleChannelCommand(
    Guid NotificationRuleId,
    string NewChannel) : ICommand;
