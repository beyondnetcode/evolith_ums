namespace Evolith.Ums.Application.Identity.Tenant.Branch.Commands;

using FluentValidation;

public sealed class ReactivateBranchCommandValidator : AbstractValidator<ReactivateBranchCommand>
{
    public ReactivateBranchCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.BranchId)
            .NotEmpty();
    }
}
