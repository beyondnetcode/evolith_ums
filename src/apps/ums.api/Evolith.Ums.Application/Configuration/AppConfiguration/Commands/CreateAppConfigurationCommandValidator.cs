namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

using FluentValidation;

public sealed class CreateAppConfigurationCommandValidator : AbstractValidator<CreateAppConfigurationCommand>
{
    public CreateAppConfigurationCommandValidator()
    {
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Value)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(1000);
    }
}
