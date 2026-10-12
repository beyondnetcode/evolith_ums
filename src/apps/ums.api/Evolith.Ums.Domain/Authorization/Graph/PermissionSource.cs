namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// Identifies whether a resolved permission came from the base template
/// or from an explicit profile-level override.
/// </summary>
public enum PermissionSource
{
    /// <summary>Permission derives from the assigned PermissionTemplate item.</summary>
    Template,

    /// <summary>Permission was explicitly overridden at the profile level (Allow/Deny/Neutral).</summary>
    Override
}
