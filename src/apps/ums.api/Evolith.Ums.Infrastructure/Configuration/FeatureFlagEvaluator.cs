using System.Text.Json;
using Evolith.Ums.Domain.Configuration.FeatureFlag;

namespace Evolith.Ums.Infrastructure.Configuration;

public sealed class FeatureFlagEvaluator : IFeatureFlagEvaluator
{
    public FlagEvaluationResult Evaluate(FeatureFlag flag, EvaluationContext context)
    {
        // Fail-closed lifecycle (G-048): only an Active flag can resolve as enabled.
        // A non-Active flag (Inactive/Archived) always evaluates as disabled.
        if (flag.Status != FlagStatus.Active)
            return new FlagEvaluationResult(false, null, $"Flag is not active (status: {flag.Status.Name})");

        // All targeting criteria must match before any rollout is considered.
        var criteriaFailure = EvaluateCriteria(flag, context);
        if (criteriaFailure is not null)
            return criteriaFailure;

        // Percentage rollout (G-048): FlagType.Percentage honours RolloutPercentage
        // through deterministic per-subject bucketing.
        if (flag.FlagType == FlagType.Percentage)
            return EvaluatePercentage(flag, context);

        return flag.Criteria.Any()
            ? new FlagEvaluationResult(true, null, "All criteria matched")
            : new FlagEvaluationResult(true, null, "No restrictions — active for all");
    }

    /// <summary>
    /// Evaluates targeting criteria. Returns a disabled result when a criteria group
    /// fails to match; returns <c>null</c> when there are no criteria or all groups pass.
    /// </summary>
    private static FlagEvaluationResult? EvaluateCriteria(FeatureFlag flag, EvaluationContext context)
    {
        if (!flag.Criteria.Any())
            return null;

        var groups = flag.Criteria.GroupBy(c => c.CriteriaType);

        foreach (var group in groups)
        {
            var contextValue = GetContextValue(context, group.Key);

            if (contextValue is null)
                return new FlagEvaluationResult(false, group.Key, $"Context missing value for {group.Key}");

            var groupPassed = false;
            foreach (var criterion in group)
            {
                if (ApplyOperator(criterion.Operator, contextValue, criterion.Value))
                {
                    groupPassed = true;
                    break;
                }
            }

            if (!groupPassed)
                return new FlagEvaluationResult(false, group.Key, $"No match for CriteriaType {group.Key}");
        }

        return null;
    }

    /// <summary>
    /// Applies the percentage rollout for <see cref="FlagType.Percentage"/> flags.
    /// Uses a stable per-subject bucket so a given subject is consistently in or out.
    /// Fail-closed: when no identifying context is available the flag is disabled.
    /// </summary>
    private static FlagEvaluationResult EvaluatePercentage(FeatureFlag flag, EvaluationContext context)
    {
        var percentage = flag.RolloutPercentage ?? 0;

        if (percentage <= 0)
            return new FlagEvaluationResult(false, null, "Rollout percentage is 0");
        if (percentage >= 100)
            return new FlagEvaluationResult(true, null, "Rollout percentage is 100");

        var subject = context.ProfileId?.ToString()
                      ?? context.TenantId?.ToString()
                      ?? context.BranchId?.ToString()
                      ?? context.RoleCode
                      ?? context.Environment;

        if (string.IsNullOrEmpty(subject))
            return new FlagEvaluationResult(false, null, "Percentage rollout requires identifying context (fail-closed)");

        var bucket = ComputeBucket($"{flag.FlagCode}:{subject}");
        return bucket < percentage
            ? new FlagEvaluationResult(true, null, $"In rollout: bucket {bucket} < {percentage}%")
            : new FlagEvaluationResult(false, null, $"Out of rollout: bucket {bucket} >= {percentage}%");
    }

    /// <summary>
    /// Deterministic 0..99 bucket via FNV-1a — independent of the process hash seed,
    /// so the same subject always lands in the same bucket across runs and nodes.
    /// </summary>
    private static int ComputeBucket(string key)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(key))
        {
            hash ^= b;
            hash *= prime;
        }

        return (int)(hash % 100);
    }

    private static string? GetContextValue(EvaluationContext context, string criteriaType) =>
        criteriaType switch
        {
            "TenantId"       => context.TenantId?.ToString(),
            "BranchId"       => context.BranchId?.ToString(),
            "UserProfileId"  => context.ProfileId?.ToString(),
            "RoleCode"       => context.RoleCode,
            "Environment"    => context.Environment,
            _                => context.CustomAttributes?.GetValueOrDefault(criteriaType),
        };

    private static bool ApplyOperator(string @operator, string contextValue, string criteriaValue) =>
        @operator switch
        {
            "Equals"           => string.Equals(contextValue, criteriaValue, StringComparison.OrdinalIgnoreCase),
            "NotEquals"        => !string.Equals(contextValue, criteriaValue, StringComparison.OrdinalIgnoreCase),
            "In"               => TryDeserializeArray(criteriaValue)?.Any(v => string.Equals(v, contextValue, StringComparison.OrdinalIgnoreCase)) ?? false,
            "NotIn"            => !(TryDeserializeArray(criteriaValue)?.Any(v => string.Equals(v, contextValue, StringComparison.OrdinalIgnoreCase)) ?? false),
            "LessThanOrEqual"  => int.TryParse(criteriaValue, out var threshold) && int.TryParse(contextValue, out var val) && val <= threshold,
            "Between"          => EvaluateDateRange(contextValue, criteriaValue),
            _                  => false,
        };

    private static string[]? TryDeserializeArray(string value)
    {
        try { return JsonSerializer.Deserialize<string[]>(value); }
        catch { return null; }
    }

    private static bool EvaluateDateRange(string contextValue, string criteriaValue)
    {
        try
        {
            var range = JsonSerializer.Deserialize<DateRangeValue>(criteriaValue);
            if (range is null) return false;
            var current = DateTime.Parse(contextValue, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
            return current >= range.From && current <= range.To;
        }
        catch { return false; }
    }

    private sealed record DateRangeValue(DateTime From, DateTime To);
}
