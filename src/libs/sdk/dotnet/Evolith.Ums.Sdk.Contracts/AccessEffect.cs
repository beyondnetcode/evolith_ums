using System.Text.Json.Serialization;

namespace Evolith.Ums.Sdk.Contracts;

/// <summary>
/// Resolved authorization effect for a single (target, action) pair in the AuthorizationGraph.
/// Three-valued enum: explicit Allow, explicit Deny, or NotGranted (implicit deny — no entry exists).
/// Deny always wins over Allow (Axiom A3).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccessEffect
{
    Allow = 0,
    Deny = 1,
    NotGranted = 2
}

/// <summary>
/// Where a resolved permission originated: from the bound PermissionTemplate item or from an explicit
/// per-Profile override. Override takes precedence over Template.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PermissionSource
{
    Template = 0,
    Override = 1
}
