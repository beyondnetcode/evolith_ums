namespace Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;

public sealed record DelegationDto(
    Guid DelegationId,
    Guid TenantId,
    Guid DelegatingAdminId,
    Guid DelegatedAdminId,
    string ScopeType,
    Guid? ScopeId,
    IReadOnlyList<string> AllowedActions,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int? MaxDurationDays,
    bool RequiresApproval,
    Guid? ApprovalRequestId,
    string Status,
    DateTimeOffset? RevokedAt,
    Guid? RevokedBy,
    string? RevocationReason);
