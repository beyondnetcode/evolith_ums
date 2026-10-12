namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

using FluentValidation;

public sealed class EvaluateFeatureFlagCommandValidator : AbstractValidator<EvaluateFeatureFlagCommand>
{
    public EvaluateFeatureFlagCommandValidator()
    {
        RuleFor(command => command.FeatureFlagId).NotEmpty();
    }
}
