namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class RecordAuthenticationAttemptCommandValidator
    : AbstractValidator<RecordAuthenticationAttemptCommand>
{
    public RecordAuthenticationAttemptCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.IpAddress).NotEmpty().MaximumLength(45); // IPv6 max length
    }
}
