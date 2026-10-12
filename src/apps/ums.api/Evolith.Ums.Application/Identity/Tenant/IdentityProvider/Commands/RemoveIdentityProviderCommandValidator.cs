namespace Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Commands;

using FluentValidation;

public sealed class RemoveIdentityProviderCommandValidator : AbstractValidator<RemoveIdentityProviderCommand>
{
    public RemoveIdentityProviderCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.IdentityProviderId)
            .NotEmpty();
    }
}
