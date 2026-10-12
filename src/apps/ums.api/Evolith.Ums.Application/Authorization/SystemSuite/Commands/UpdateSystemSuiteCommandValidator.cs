namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class UpdateSystemSuiteCommandValidator : AbstractValidator<UpdateSystemSuiteCommand>
{
    public UpdateSystemSuiteCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
