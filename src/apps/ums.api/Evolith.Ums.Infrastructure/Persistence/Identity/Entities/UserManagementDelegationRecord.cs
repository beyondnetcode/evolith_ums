using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

public sealed class UserManagementDelegationRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DelegatingAdminId { get; set; }
    public Guid DelegatedAdminId { get; set; }
    public int ScopeTypeId { get; set; }
    public Guid? ScopeId { get; set; }
    public string AllowedActionsJson { get; set; } = string.Empty;
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public int? MaxDurationDays { get; set; }
    public bool RequiresApproval { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public int StatusId { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
