using FluentValidation;

namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

public sealed class AddDomainResourceCommandValidator : AbstractValidator<AddDomainResourceCommand>
{
    public AddDomainResourceCommandValidator()
    {
        RuleFor(x => x.SystemSuiteId)
            .NotEmpty().WithMessage("System Suite ID is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(100).WithMessage("Code cannot exceed 100 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.");

        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required.")
            .Must(t => t == "Aggregate" || t == "Entity" || t == "DomainMethod")
            .WithMessage("Type must be 'Aggregate', 'Entity', or 'DomainMethod'.");

        RuleFor(x => x.ParentResourceId)
            .NotEmpty().WithMessage("ParentResourceId is required for DomainMethod resources.")
            .When(x => x.Type == "DomainMethod");
    }
}
