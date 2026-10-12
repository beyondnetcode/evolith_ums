namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

public sealed record DeactivateFeatureFlagCommand(Guid FeatureFlagId) : ICommand;
