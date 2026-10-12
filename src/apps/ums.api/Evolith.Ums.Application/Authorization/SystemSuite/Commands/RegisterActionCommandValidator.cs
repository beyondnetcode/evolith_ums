namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class RegisterActionCommandValidator : AbstractValidator<RegisterActionCommand>
{
    public RegisterActionCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
