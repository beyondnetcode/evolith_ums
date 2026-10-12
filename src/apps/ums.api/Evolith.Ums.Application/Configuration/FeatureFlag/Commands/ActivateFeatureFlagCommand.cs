namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

public sealed record ActivateFeatureFlagCommand(Guid FeatureFlagId) : ICommand;
