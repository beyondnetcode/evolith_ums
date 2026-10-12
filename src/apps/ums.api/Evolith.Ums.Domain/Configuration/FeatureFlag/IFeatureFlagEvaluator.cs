namespace Evolith.Ums.Domain.Configuration.FeatureFlag;

public sealed record EvaluationContext(
    Guid? TenantId = null,
    Guid? BranchId = null,
    Guid? ProfileId = null,
    string? RoleCode = null,
    string? Environment = null,
    Dictionary<string, string>? CustomAttributes = null);

public sealed record FlagEvaluationResult(bool IsEnabled, string? MatchedCriteriaType, string? Reason);

public interface IFeatureFlagEvaluator
{
    FlagEvaluationResult Evaluate(FeatureFlag flag, EvaluationContext context);
}
