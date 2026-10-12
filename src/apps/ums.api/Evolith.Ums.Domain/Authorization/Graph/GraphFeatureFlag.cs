using System.Text.Json.Serialization;

namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Result of evaluating a FeatureFlag against the authenticated user's context.
/// The evaluation is done at auth-time so the client can use the result without
/// re-querying UMS on every request.
/// </summary>
public sealed record GraphFeatureFlag(
    string  FlagCode,
    [property: JsonIgnore] Guid SystemSuiteId,
    bool    IsEnabled,
    string? MatchedCriteriaType);   // e.g. "TenantId", "RoleCode", null when no criteria
