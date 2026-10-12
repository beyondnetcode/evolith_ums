namespace Evolith.Ums.Domain.Authorization.Graph;

/// <summary>
/// The effective permission result for a specific (target, action) pair
/// after applying template items, profile overrides, and deny-wins semantics.
/// </summary>
public enum AccessEffect
{
    /// <summary>The user is explicitly allowed to perform this action.</summary>
    Allow,

    /// <summary>The user is explicitly denied. Deny always overrides Allow.</summary>
    Deny,

    /// <summary>No permission entry exists for this (target, action) — implicit denial.</summary>
    NotGranted
}
