namespace Evolith.Ums.Domain.Identity.UserManagementDelegation;

public class UserManagementDelegationProps : IProps
{
    public DelegationId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public UserAccountId DelegatingAdminId { get; private set; }
    public UserAccountId DelegatedAdminId { get; private set; }
    public DelegationScopeType ScopeType { get; private set; }
    public Guid? ScopeId { get; private set; }
    public IReadOnlyList<DelegatedAction> AllowedActions { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ValidUntil { get; private set; }
    public int? MaxDurationDays { get; private set; }
    public bool RequiresApproval { get; private set; }
    public Guid? ApprovalRequestId { get; private set; }
    public DelegationStatus Status { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public string? RevocationReason { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public UserManagementDelegationProps(
        DelegationId id,
        TenantId tenantId,
        UserAccountId delegatingAdminId,
        UserAccountId delegatedAdminId,
        DelegationScopeType scopeType,
        Guid? scopeId,
        IReadOnlyList<DelegatedAction> allowedActions,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        int? maxDurationDays,
        bool requiresApproval,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        DelegatingAdminId = delegatingAdminId;
        DelegatedAdminId = delegatedAdminId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        AllowedActions = allowedActions;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        MaxDurationDays = maxDurationDays;
        RequiresApproval = requiresApproval;
        Status = DelegationStatus.Draft;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public UserManagementDelegationProps(
        DelegationId id,
        TenantId tenantId,
        UserAccountId delegatingAdminId,
        UserAccountId delegatedAdminId,
        DelegationScopeType scopeType,
        Guid? scopeId,
        IReadOnlyList<DelegatedAction> allowedActions,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        int? maxDurationDays,
        bool requiresApproval,
        DelegationStatus status,
        Guid? approvalRequestId,
        DateTimeOffset? revokedAt,
        Guid? revokedBy,
        string? revocationReason,
        AuditValueObject audit)
    {
        Id = id;
        TenantId = tenantId;
        DelegatingAdminId = delegatingAdminId;
        DelegatedAdminId = delegatedAdminId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        AllowedActions = allowedActions;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        MaxDurationDays = maxDurationDays;
        RequiresApproval = requiresApproval;
        Status = status;
        ApprovalRequestId = approvalRequestId;
        RevokedAt = revokedAt;
        RevokedBy = revokedBy;
        RevocationReason = revocationReason;
        Audit = audit;
    }

    public UserManagementDelegationProps WithStatus(DelegationStatus status)
    {
        var clone = (UserManagementDelegationProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    public UserManagementDelegationProps WithApprovalRequestId(Guid? approvalRequestId)
    {
        var clone = (UserManagementDelegationProps)MemberwiseClone();
        clone.ApprovalRequestId = approvalRequestId;
        return clone;
    }

    public UserManagementDelegationProps WithRevocationReason(string? revocationReason)
    {
        var clone = (UserManagementDelegationProps)MemberwiseClone();
        clone.RevocationReason = revocationReason;
        return clone;
    }

    public UserManagementDelegationProps WithRevokedAt(DateTimeOffset? revokedAt)
    {
        var clone = (UserManagementDelegationProps)MemberwiseClone();
        clone.RevokedAt = revokedAt;
        return clone;
    }

    public UserManagementDelegationProps WithRevokedBy(Guid? revokedBy)
    {
        var clone = (UserManagementDelegationProps)MemberwiseClone();
        clone.RevokedBy = revokedBy;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}