namespace Evolith.Ums.Application.Audit.AuditRecord.DTOs;

public sealed record AuditRecordDto(
    Guid AuditRecordId,
    Guid WhoActed,
    string SubjectType,
    DateTime WhenOccurred,
    string WhatChanged,
    string EventType,
    string AuditResult,
    Guid AffectedEntityId,
    string AffectedEntityType,
    Guid RootTenantId,
    string? Metadata);
