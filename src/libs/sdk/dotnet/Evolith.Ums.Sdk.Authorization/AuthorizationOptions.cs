namespace Evolith.Ums.Sdk.Authorization;

/// <summary>
/// Options controlling SDK-wide authorization behavior. Bound from configuration via
/// <c>services.Configure&lt;AuthorizationOptions&gt;(...)</c>.
/// </summary>
public sealed class AuthorizationOptions
{
    /// <summary>Global enforcement mode. Default: <see cref="AuthorizationMode.Enforce"/>.</summary>
    public AuthorizationMode Mode { get; set; } = AuthorizationMode.Enforce;
}

public enum AuthorizationMode
{
    /// <summary>Denials block execution (throw or return failure depending on attribute).</summary>
    Enforce = 0,

    /// <summary>Denials are logged but never block execution. Used for progressive rollouts.</summary>
    AuditOnly = 1
}
