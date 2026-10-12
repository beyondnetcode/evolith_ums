namespace Evolith.Ums.Application.Approvals.NotificationRule.Commands;

using FluentValidation;

public sealed class CreateNotificationRuleCommandValidator : AbstractValidator<CreateNotificationRuleCommand>
{
    public CreateNotificationRuleCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.Channel).NotEmpty();
        RuleFor(c => c.Recipient).NotEmpty().MaximumLength(250);
    }
}
