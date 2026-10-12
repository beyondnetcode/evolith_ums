namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

using FluentValidation;

public sealed class ApproveDelegationCommandValidator : AbstractValidator<ApproveDelegationCommand>
{
    public ApproveDelegationCommandValidator()
    {
        RuleFor(command => command.DelegationId)
            .NotEmpty();
    }
}
