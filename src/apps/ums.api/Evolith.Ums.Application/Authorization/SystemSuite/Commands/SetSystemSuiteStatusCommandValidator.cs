namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class SetSystemSuiteStatusCommandValidator : AbstractValidator<SetSystemSuiteStatusCommand>
{
    public SetSystemSuiteStatusCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.Status).NotEmpty().MaximumLength(20);
    }
}
