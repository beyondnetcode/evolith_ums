using Evolith.Ums.Application.Audit.AuditRecord.DTOs;

namespace Evolith.Ums.Application.Audit.AuditRecord.Commands;

public sealed record RecordAuditCommand(
    Guid WhoActed,
    string SubjectType,
    string WhatChanged,
    string EventType,
    string AuditResult,
    Guid AffectedEntityId,
    string AffectedEntityType,
    Guid RootTenantId,
    string? Metadata) : ICommand<RecordAuditResponse>;
