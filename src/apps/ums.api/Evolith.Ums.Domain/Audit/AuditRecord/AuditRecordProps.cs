namespace Evolith.Ums.Domain.Audit.AuditRecord;

public class AuditRecordProps : IProps
{
    public IdValueObject Id { get; set; }
    public Guid WhoActed { get; set; }
    public SubjectType SubjectType { get; set; }
    public DateTime WhenOccurred { get; set; }
    public string WhatChanged { get; set; }
    public string EventType { get; set; }
    public AuditResult AuditResult { get; set; }
    public Guid AffectedEntityId { get; set; }
    public string AffectedEntityType { get; set; }
    public Guid RootTenantId { get; set; }
    public string? Metadata { get; set; }

    public AuditRecordProps(
        IdValueObject id,
        Guid whoActed,
        SubjectType subjectType,
        string whatChanged,
        string eventType,
        AuditResult auditResult,
        Guid affectedEntityId,
        string affectedEntityType,
        Guid rootTenantId,
        string? metadata)
    {
        Id = id;
        WhoActed = whoActed;
        SubjectType = subjectType;
        WhenOccurred = DateTime.UtcNow;
        WhatChanged = whatChanged;
        EventType = eventType;
        AuditResult = auditResult;
        AffectedEntityId = affectedEntityId;
        AffectedEntityType = affectedEntityType;
        RootTenantId = rootTenantId;
        Metadata = metadata;
    }

    public object Clone() => MemberwiseClone();
}
