namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

using FluentValidation;

public sealed class RejectDelegationCommandValidator : AbstractValidator<RejectDelegationCommand>
{
    public RejectDelegationCommandValidator()
    {
        RuleFor(command => command.DelegationId)
            .NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
