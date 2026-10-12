using BeyondNetCode.Shell.Aop;

namespace Evolith.Ums.Sdk.Authorization.Aop;

/// <summary>
/// Abstract base for all UMS SDK authorization attributes. Inherits from the Shell.Aop attribute
/// marker so the aspect chain selects this attribute family.
/// </summary>
public abstract class RequiresAuthorizationAttribute : AbstractAspectAttribute
{
    /// <summary>How to react on denial. Default: Throw.</summary>
    public DenialBehavior OnDenied { get; set; } = DenialBehavior.Throw;

    /// <summary>When true, denials are logged but never block (overrides global Enforce mode).</summary>
    public bool AuditOnly { get; set; } = false;

    /// <summary>Primitive identifier used in diagnostics and decisions.</summary>
    public abstract string Primitive { get; }

    /// <summary>Target identifier (scope, option code, etc.) used in diagnostics.</summary>
    public abstract string Target { get; }
}
