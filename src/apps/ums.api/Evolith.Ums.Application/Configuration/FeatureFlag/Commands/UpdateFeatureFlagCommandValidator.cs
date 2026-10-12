namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

using FluentValidation;

public sealed class UpdateFeatureFlagCommandValidator : AbstractValidator<UpdateFeatureFlagCommand>
{
    public UpdateFeatureFlagCommandValidator()
    {
        RuleFor(command => command.FeatureFlagId).NotEmpty();

        RuleFor(command => command.FlagTargets)
            .NotNull()
            .MaximumLength(2000);

        RuleFor(command => command.RolloutPercentage)
            .InclusiveBetween(0, 100)
            .When(command => command.RolloutPercentage.HasValue);
    }
}
