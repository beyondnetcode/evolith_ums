using System;

namespace Evolith.Ums.Infrastructure.Persistence.Audit.Entities;

public sealed class AuditRecordRecord : IAuditableRecord
{
    public Guid Id { get; set; }
    public Guid WhoActed { get; set; }
    public int SubjectTypeId { get; set; }
    public DateTime WhenOccurred { get; set; }
    public string WhatChanged { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int AuditResultId { get; set; }
    public Guid AffectedEntityId { get; set; }
    public string AffectedEntityType { get; set; } = string.Empty;
    public Guid RootTenantId { get; set; }
    public string? Metadata { get; set; }

    // IAuditableRecord mapping
    public string CreatedBy
    {
        get => WhoActed.ToString();
        set => WhoActed = Guid.TryParse(value, out var g) ? g : Guid.Empty;
    }

    public DateTime CreatedAtUtc
    {
        get => WhenOccurred;
        set => WhenOccurred = value;
    }

    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string AuditTimeSpan { get; set; } = string.Empty;
}
