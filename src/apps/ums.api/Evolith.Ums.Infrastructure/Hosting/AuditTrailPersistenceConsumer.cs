using MassTransit;
using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Domain.Audit.AuditRecord;

namespace Evolith.Ums.Infrastructure.Hosting;

/// <summary>
/// G-040: consumidor que persiste el registro de auditoría automática entregado POST-commit por el
/// Transactional Outbox de MassTransit. Reemplaza el <c>BackgroundService</c> que leía un canal en
/// memoria (con descarte silencioso). Persistencia append-only vía <see cref="IAuditRecordRepository"/>
/// (ADR-0016). La idempotencia ante reentrega la aporta el inbox EF de MassTransit.
/// </summary>
public sealed class AuditTrailPersistenceConsumer(
    IAuditRecordRepository repository,
    ILogger<AuditTrailPersistenceConsumer> logger) : IConsumer<AuditTrailEntry>
{
    public async Task Consume(ConsumeContext<AuditTrailEntry> context)
    {
        var entry = context.Message;

        var subjectType = ResolveSubjectType(entry.SubjectType);
        var auditResult = ResolveAuditResult(entry.AuditResult);

        var auditRecordResult = AuditRecord.Record(
            entry.WhoActed,
            subjectType,
            entry.WhatChanged,
            entry.EventType,
            auditResult,
            entry.AffectedEntityId,
            entry.AffectedEntityType,
            entry.RootTenantId,
            entry.Metadata);

        if (auditRecordResult.IsFailure)
        {
            logger.LogWarning(
                "AuditTrailPersistence: descarta registro de auditoría inválido {EventType} para {AffectedEntityType}/{AffectedEntityId}: {Error}",
                entry.EventType,
                entry.AffectedEntityType,
                entry.AffectedEntityId,
                auditRecordResult.Error);
            return;
        }

        await repository.AppendAsync(auditRecordResult.Value, context.CancellationToken);
        await repository.UnitOfWork.SaveEntitiesAsync(context.CancellationToken);
    }

    private static SubjectType ResolveSubjectType(string value)
        => value switch
        {
            nameof(SubjectType.User) => SubjectType.User,
            nameof(SubjectType.Admin) => SubjectType.Admin,
            nameof(SubjectType.System) => SubjectType.System,
            "BACKGROUND_WORKER" => SubjectType.BackgroundWorker,
            _ => SubjectType.System,
        };

    private static AuditResult ResolveAuditResult(string value)
        => value switch
        {
            nameof(AuditResult.Failure) => AuditResult.Failure,
            nameof(AuditResult.Partial) => AuditResult.Partial,
            _ => AuditResult.Success,
        };
}
