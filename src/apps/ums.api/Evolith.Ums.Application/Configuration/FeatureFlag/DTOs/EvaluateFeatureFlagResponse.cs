namespace Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

public sealed record EvaluateFeatureFlagResponse(
    Guid FeatureFlagId,
    string FlagCode,
    bool IsEnabled,
    string? MatchedCriteriaType,
    string? Reason);
