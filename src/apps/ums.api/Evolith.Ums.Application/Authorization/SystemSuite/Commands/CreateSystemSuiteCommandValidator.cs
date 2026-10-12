namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using FluentValidation;

public sealed class CreateSystemSuiteCommandValidator : AbstractValidator<CreateSystemSuiteCommand>
{
    public CreateSystemSuiteCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(500);
    }
}
