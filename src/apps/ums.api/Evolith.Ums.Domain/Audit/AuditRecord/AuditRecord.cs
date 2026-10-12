namespace Evolith.Ums.Domain.Audit.AuditRecord;

public sealed class AuditRecord : AggregateRoot<AuditRecord, AuditRecordProps>
{
    private AuditRecord(AuditRecordProps props) : base(props) { }

    public Guid WhoActed => Props.WhoActed;
    public SubjectType SubjectType => Props.SubjectType;
    public DateTime WhenOccurred => Props.WhenOccurred;
    public string WhatChanged => Props.WhatChanged;
    public string EventType => Props.EventType;
    public AuditResult AuditResult => Props.AuditResult;
    public Guid AffectedEntityId => Props.AffectedEntityId;
    public string AffectedEntityType => Props.AffectedEntityType;
    public Guid RootTenantId => Props.RootTenantId;
    public string? Metadata => Props.Metadata;

    public AuditRecordId GetId() => AuditRecordId.Load(Props.Id.GetValue());

    // INV-AU2: all four required fields must be non-empty
    public static Result<AuditRecord> Record(
        Guid whoActed,
        SubjectType subjectType,
        string whatChanged,
        string eventType,
        AuditResult auditResult,
        Guid affectedEntityId,
        string affectedEntityType,
        Guid rootTenantId,
        string? metadata = null)
    {
        if (whoActed == Guid.Empty)
            return Result<AuditRecord>.Failure(DomainErrors.Audit.WhatChangedRequired);

        if (string.IsNullOrWhiteSpace(whatChanged))
            return Result<AuditRecord>.Failure(DomainErrors.Audit.WhatChangedRequired);

        if (affectedEntityId == Guid.Empty || string.IsNullOrWhiteSpace(affectedEntityType))
            return Result<AuditRecord>.Failure(DomainErrors.Audit.AffectedEntityRequired);

        var props = new AuditRecordProps(
            IdValueObject.Create(), whoActed, subjectType, whatChanged,
            eventType, auditResult, affectedEntityId, affectedEntityType, rootTenantId, metadata);

        var record = new AuditRecord(props);

        if (!record.IsValid())
            return Result<AuditRecord>.Failure(record.BrokenRules.GetBrokenRulesAsString());

        return Result<AuditRecord>.Success(record);
    }

    // INV-AU1: no mutation methods — append-only
}
