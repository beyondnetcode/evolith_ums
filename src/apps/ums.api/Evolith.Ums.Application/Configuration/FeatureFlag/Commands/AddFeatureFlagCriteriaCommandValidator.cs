namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

using FluentValidation;

public sealed class AddFeatureFlagCriteriaCommandValidator : AbstractValidator<AddFeatureFlagCriteriaCommand>
{
    public AddFeatureFlagCriteriaCommandValidator()
    {
        RuleFor(command => command.FeatureFlagId).NotEmpty();
        RuleFor(command => command.CriteriaType).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Operator).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Value).NotEmpty().MaximumLength(2000);
    }
}
