using System;
using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;

public sealed class ApprovalRequestRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid TargetUserId { get; set; }
    public Guid? TargetProfileId { get; set; }
    public int StatusId { get; set; }
    public Guid RequestedSystemId { get; set; }
    public Guid? RequestedBranchId { get; set; }
    public Guid RequestedRoleId { get; set; }
    public string? Justification { get; set; }
    public Guid? GrantedRoleId { get; set; }
    public string? DecisionReason { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];
}
