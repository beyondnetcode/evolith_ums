namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

public sealed record UpdateFeatureFlagCommand(
    Guid FeatureFlagId,
    string FlagTargets,
    int? RolloutPercentage) : ICommand;
