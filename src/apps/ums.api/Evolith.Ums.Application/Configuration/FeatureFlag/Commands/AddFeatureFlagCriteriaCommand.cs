using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

public sealed record AddFeatureFlagCriteriaCommand(
    Guid FeatureFlagId,
    string CriteriaType,
    string Operator,
    string Value) : ICommand<AddFeatureFlagCriteriaResponse>;
