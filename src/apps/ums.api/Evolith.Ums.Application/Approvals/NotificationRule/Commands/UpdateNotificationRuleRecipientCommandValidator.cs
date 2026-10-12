namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;
public sealed class UpdateNotificationRuleRecipientCommandValidator : AbstractValidator<UpdateNotificationRuleRecipientCommand>
{
    public UpdateNotificationRuleRecipientCommandValidator() { RuleFor(x => x.NotificationRuleId).NotEmpty(); RuleFor(x => x.NewRecipient).NotEmpty().MaximumLength(255); }
}
