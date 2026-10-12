namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

using FluentValidation;

public sealed class UpdateAppConfigurationCommandValidator : AbstractValidator<UpdateAppConfigurationCommand>
{
    public UpdateAppConfigurationCommandValidator()
    {
        RuleFor(command => command.AppConfigurationId).NotEmpty();
        RuleFor(command => command.Value).NotEmpty().MaximumLength(4000);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
    }
}
