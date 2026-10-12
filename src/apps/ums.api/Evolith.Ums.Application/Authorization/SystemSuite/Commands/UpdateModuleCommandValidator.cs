namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using FluentValidation;

public sealed class UpdateModuleCommandValidator : AbstractValidator<UpdateModuleCommand>
{
    public UpdateModuleCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId)
            .NotEmpty();

        RuleFor(x => x.ModuleId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(150);

        // Description is optional — the domain allows empty strings.
        RuleFor(x => x.Description)
            .MaximumLength(500);

        RuleFor(x => x.SortOrder)
            .GreaterThan(0)
            .WithMessage("SortOrder must be a positive integer.");
    }
}
