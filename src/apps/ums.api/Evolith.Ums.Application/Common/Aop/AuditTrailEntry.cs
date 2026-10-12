namespace Evolith.Ums.Application.Common.Aop;

public sealed record AuditTrailEntry(
    Guid WhoActed,
    string SubjectType,
    string WhatChanged,
    string EventType,
    string AuditResult,
    Guid AffectedEntityId,
    string AffectedEntityType,
    Guid RootTenantId,
    string? Metadata);
