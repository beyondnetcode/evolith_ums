namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;
public sealed record UpdateNotificationRuleRecipientCommand(Guid NotificationRuleId, string NewRecipient) : ICommand;
