namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

using FluentValidation;

public sealed class SubmitDelegationForApprovalCommandValidator : AbstractValidator<SubmitDelegationForApprovalCommand>
{
    public SubmitDelegationForApprovalCommandValidator()
    {
        RuleFor(command => command.DelegationId)
            .NotEmpty();
    }
}
