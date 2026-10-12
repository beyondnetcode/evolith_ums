namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;

using FluentValidation;

public sealed class CloseBranchCommandValidator : AbstractValidator<CloseBranchCommand>
{
    public CloseBranchCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.BranchId)
            .NotEmpty();

        // El motivo es opcional, pero si viene debe caber en la columna de la bitácora: mejor un 400
        // explicable que un truncamiento silencioso del único texto que explica el cierre.
        RuleFor(command => command.Reason)
            .MaximumLength(500)
            .When(command => command.Reason is not null);
    }
}
