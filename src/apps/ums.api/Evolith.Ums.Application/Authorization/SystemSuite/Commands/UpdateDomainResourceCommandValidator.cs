using FluentValidation;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class UpdateDomainResourceCommandValidator : AbstractValidator<UpdateDomainResourceCommand>
{
    public UpdateDomainResourceCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId)
            .NotEmpty().WithMessage("System Suite ID is required.");

        RuleFor(x => x.DomainResourceId)
            .NotEmpty().WithMessage("Domain Resource ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");
    }
}
