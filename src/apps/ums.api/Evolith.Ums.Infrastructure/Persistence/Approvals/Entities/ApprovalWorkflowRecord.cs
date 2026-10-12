using System;
using System.Collections.Generic;
using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;

public sealed class ApprovalWorkflowRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SystemSuiteId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TargetUserCategoryId { get; set; }
    public bool RequiresApproval { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];

    public List<ApprovalRequiredDocumentRecord> RequiredDocuments { get; set; } = [];
}
