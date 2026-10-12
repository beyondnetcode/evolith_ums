using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

public sealed record EvaluateFeatureFlagCommand(
    Guid FeatureFlagId,
    Guid? TenantId = null,
    Guid? BranchId = null,
    Guid? ProfileId = null,
    string? RoleCode = null,
    string? Environment = null,
    Dictionary<string, string>? CustomAttributes = null) : ICommand<EvaluateFeatureFlagResponse>;
