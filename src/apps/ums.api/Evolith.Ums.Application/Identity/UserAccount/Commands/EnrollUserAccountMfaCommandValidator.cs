namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class EnrollUserAccountMfaCommandValidator : AbstractValidator<EnrollUserAccountMfaCommand>
{
    public EnrollUserAccountMfaCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.Method)
            .NotEmpty()
            .Must(method => DomainEnumerationParser.FromName<MfaMethod>(method) is not null)
            .WithMessage("A valid MFA method is required.");
    }
}
