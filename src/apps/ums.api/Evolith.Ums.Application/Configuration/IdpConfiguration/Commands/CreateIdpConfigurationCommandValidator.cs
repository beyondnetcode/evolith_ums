namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

using FluentValidation;
using Evolith.Ums.Domain.Enums;

public sealed class CreateIdpConfigurationCommandValidator : AbstractValidator<CreateIdpConfigurationCommand>
{
    public CreateIdpConfigurationCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.SystemSuiteId).NotEmpty();

        RuleFor(command => command.ProviderType)
            .NotEmpty()
            .Must(type => DomainEnumerationParser.FromName<ProviderType>(type) is not null)
            .WithMessage("Identity provider type is not supported.");

        RuleFor(command => command.ConfigPayload).NotEmpty().MaximumLength(20000);
        RuleFor(command => command.SecretRef).NotEmpty().MaximumLength(500);
        RuleFor(command => command.ResolutionPriority).GreaterThanOrEqualTo(0);
    }
}
