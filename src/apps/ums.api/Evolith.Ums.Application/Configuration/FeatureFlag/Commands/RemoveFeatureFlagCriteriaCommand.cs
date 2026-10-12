namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

public sealed record RemoveFeatureFlagCriteriaCommand(Guid FeatureFlagId, Guid CriteriaId) : ICommand;
