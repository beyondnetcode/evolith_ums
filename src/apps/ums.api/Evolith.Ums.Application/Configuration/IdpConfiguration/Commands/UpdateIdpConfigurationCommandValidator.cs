namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

using FluentValidation;

public sealed class UpdateIdpConfigurationCommandValidator : AbstractValidator<UpdateIdpConfigurationCommand>
{
    public UpdateIdpConfigurationCommandValidator()
    {
        RuleFor(command => command.IdpConfigurationId).NotEmpty();
        RuleFor(command => command.ConfigPayload).NotEmpty().MaximumLength(20000);
        RuleFor(command => command.SecretRef).NotEmpty().MaximumLength(500);
    }
}
