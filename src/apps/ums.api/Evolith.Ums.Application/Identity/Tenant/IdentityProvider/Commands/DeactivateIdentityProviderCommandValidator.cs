namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Commands;

using FluentValidation;

public sealed class DeactivateIdentityProviderCommandValidator : AbstractValidator<DeactivateIdentityProviderCommand>
{
    public DeactivateIdentityProviderCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.IdentityProviderId)
            .NotEmpty();
    }
}
