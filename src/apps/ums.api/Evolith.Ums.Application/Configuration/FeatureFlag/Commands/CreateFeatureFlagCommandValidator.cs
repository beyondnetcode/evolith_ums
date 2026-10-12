namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

using Evolith.Ums.Domain.Enums;
using FluentValidation;

public sealed class CreateFeatureFlagCommandValidator : AbstractValidator<CreateFeatureFlagCommand>
{
    public CreateFeatureFlagCommandValidator()
    {
        RuleFor(command => command.SystemSuiteId)
            .NotEmpty()
            .WithMessage("SystemSuiteId is required.");

        RuleFor(command => command.FlagCode)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.FlagType)
            .NotEmpty()
            .Must(type => DomainEnumerationParser.FromName<FlagType>(type) is not null)
            .WithMessage("Feature flag type is not supported.");

        RuleFor(command => command.LinkedResourceType)
            .Must(type => string.IsNullOrWhiteSpace(type) || DomainEnumerationParser.FromName<LinkedResourceType>(type) is not null)
            .WithMessage("Linked resource type is not supported.");

        RuleFor(command => command.FlagTargets)
            .NotEmpty()
            .MaximumLength(2000);

        RuleFor(command => command.RolloutPercentage)
            .InclusiveBetween(0, 100)
            .When(command => command.RolloutPercentage.HasValue);
    }
}
