using System;
using System.Collections.Generic;
using Evolith.Ums.Infrastructure.Persistence;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;

public sealed class UserDocumentRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public int StatusId { get; set; }
    public int CriticityId { get; set; }
    public string FileStoragePath { get; set; } = string.Empty;
    public string FileChecksum { get; set; } = string.Empty;
    public int NotificationStep { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = [];

    public List<AccessNotificationRecord> Notifications { get; set; } = [];
}
