using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

public sealed record CreateFeatureFlagCommand(
    Guid SystemSuiteId,
    Guid? TenantId,
    string FlagCode,
    string FlagType,
    string FlagTargets,
    string? LinkedResourceType,
    Guid? LinkedResourceId,
    int? RolloutPercentage) : ICommand<CreateFeatureFlagResponse>;
