namespace Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

public sealed record FeatureFlagDto(
    Guid FeatureFlagId,
    Guid SystemSuiteId,
    string SystemSuiteCode,
    string SystemSuiteName,
    string FlagCode,
    string FlagType,
    string FlagTargets,
    string Status,
    string? LinkedResourceType,
    Guid? LinkedResourceId,
    int? RolloutPercentage,
    IReadOnlyList<FeatureFlagCriteriaDto> Criteria);

public sealed record FeatureFlagCriteriaDto(
    Guid CriteriaId,
    string CriteriaType,
    string Operator,
    string Value,
    DateTime CreatedAtUtc);
