namespace Evolith.Ums.Sdk.Authorization;

/// <summary>
/// Outcome of an authorization probe against an AuthorizationGraph. Pure value type — no I/O,
/// no exceptions raised by construction. Throws happen at the aspect/guard layer when configured.
/// </summary>
public sealed record AuthorizationDecision
{
    public required AuthorizationDecisionStatus Status { get; init; }
    public required string Primitive { get; init; }
    public required string Target { get; init; }
    public string? ErrorCode { get; init; }
    public string? Reason { get; init; }
    public Guid? GraphRequestId { get; init; }
    public DateTimeOffset? ValidUntil { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsGranted => Status == AuthorizationDecisionStatus.Granted;
    public bool IsDenied  => Status is AuthorizationDecisionStatus.Denied
                                    or AuthorizationDecisionStatus.NotGranted
                                    or AuthorizationDecisionStatus.Expired
                                    or AuthorizationDecisionStatus.GraphMissing
                                    or AuthorizationDecisionStatus.SchemaUnsupported
                                    or AuthorizationDecisionStatus.SchemaMissing
                                    or AuthorizationDecisionStatus.TenantMismatch;

    public static AuthorizationDecision Granted(string primitive, string target,
        Guid? graphRequestId = null, DateTimeOffset? validUntil = null) =>
        new()
        {
            Status = AuthorizationDecisionStatus.Granted,
            Primitive = primitive,
            Target = target,
            GraphRequestId = graphRequestId,
            ValidUntil = validUntil
        };

    public static AuthorizationDecision Deny(string primitive, string target, string errorCode, string reason,
        Guid? graphRequestId = null, DateTimeOffset? validUntil = null) =>
        new()
        {
            Status = AuthorizationDecisionStatus.Denied,
            Primitive = primitive,
            Target = target,
            ErrorCode = errorCode,
            Reason = reason,
            GraphRequestId = graphRequestId,
            ValidUntil = validUntil
        };

    public static AuthorizationDecision NotGranted(string primitive, string target, string errorCode, string reason,
        Guid? graphRequestId = null, DateTimeOffset? validUntil = null) =>
        new()
        {
            Status = AuthorizationDecisionStatus.NotGranted,
            Primitive = primitive,
            Target = target,
            ErrorCode = errorCode,
            Reason = reason,
            GraphRequestId = graphRequestId,
            ValidUntil = validUntil
        };

    public static AuthorizationDecision Expired(string primitive, string target, DateTimeOffset validUntil) =>
        new()
        {
            Status = AuthorizationDecisionStatus.Expired,
            Primitive = primitive,
            Target = target,
            ErrorCode = "AUTH_201",
            Reason = "AuthorizationGraph.validUntil is in the past.",
            ValidUntil = validUntil
        };

    public static AuthorizationDecision GraphMissing(string primitive, string target) =>
        new()
        {
            Status = AuthorizationDecisionStatus.GraphMissing,
            Primitive = primitive,
            Target = target,
            ErrorCode = "AUTH_202",
            Reason = "No AuthorizationGraph is bound to the current scope."
        };

    public static AuthorizationDecision SchemaUnsupported(string primitive, string target, string version) =>
        new()
        {
            Status = AuthorizationDecisionStatus.SchemaUnsupported,
            Primitive = primitive,
            Target = target,
            ErrorCode = "AUTH_205",
            Reason = $"Schema version '{version}' is outside SDK compatibility range."
        };

    public static AuthorizationDecision SchemaMissing(string primitive, string target) =>
        new()
        {
            Status = AuthorizationDecisionStatus.SchemaMissing,
            Primitive = primitive,
            Target = target,
            ErrorCode = "AUTH_204",
            Reason = "AuthorizationGraph payload does not declare a schemaVersion."
        };

    public static AuthorizationDecision TenantMismatch(string expected, string actual) =>
        new()
        {
            Status = AuthorizationDecisionStatus.TenantMismatch,
            Primitive = "AssertTenant",
            Target = expected,
            ErrorCode = "AUTH_109",
            Reason = $"Tenant mismatch: expected '{expected}', got '{actual}'."
        };
}

public enum AuthorizationDecisionStatus
{
    Granted = 0,
    Denied = 1,
    NotGranted = 2,
    Expired = 3,
    GraphMissing = 4,
    SchemaUnsupported = 5,
    SchemaMissing = 6,
    TenantMismatch = 7
}
