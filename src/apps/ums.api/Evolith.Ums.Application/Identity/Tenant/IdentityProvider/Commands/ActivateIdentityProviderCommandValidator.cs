namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Commands;

using FluentValidation;

public sealed class ActivateIdentityProviderCommandValidator : AbstractValidator<ActivateIdentityProviderCommand>
{
    public ActivateIdentityProviderCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.IdentityProviderId)
            .NotEmpty();
    }
}
