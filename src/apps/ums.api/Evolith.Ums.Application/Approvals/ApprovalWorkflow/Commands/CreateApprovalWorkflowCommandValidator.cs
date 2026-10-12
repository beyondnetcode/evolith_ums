namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Commands;

using FluentValidation;

public sealed class CreateApprovalWorkflowCommandValidator : AbstractValidator<CreateApprovalWorkflowCommand>
{
    public CreateApprovalWorkflowCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(500);
        // G-045: valida el nombre del enum para evitar un NullReference → 500 cuando el
        // handler parsea un valor no vacío pero inválido con `!`.
        RuleFor(c => c.TargetUserCategory)
            .NotEmpty()
            .Must(category => DomainEnumerationParser.FromName<UserCategory>(category) is not null)
            .WithMessage("Target user category is not supported.");
    }
}
