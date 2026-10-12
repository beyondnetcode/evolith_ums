namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Commands;

using FluentValidation;

public sealed class RegisterIdentityProviderCommandValidator : AbstractValidator<RegisterIdentityProviderCommand>
{
    public RegisterIdentityProviderCommandValidator()
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

        RuleFor(command => command.Strategy)
            .NotEmpty();
    }
}
