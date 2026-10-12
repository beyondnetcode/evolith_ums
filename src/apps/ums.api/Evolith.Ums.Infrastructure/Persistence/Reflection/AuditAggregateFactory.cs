using System;
using System.Reflection;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Audit.Entities;
using BeyondNetCode.Shell.Ddd;

namespace Evolith.Ums.Infrastructure.Persistence.Reflection;

using AuditRecordAggregate = Evolith.Ums.Domain.Audit.AuditRecord.AuditRecord;

internal static class AuditAggregateFactory
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada para rehidratación de agregados. Reconstruir un agregado " +
                        "ya validado (ADR-UMS-069) desde persistencia sin exponer setters públicos preserva la pureza " +
                        "del dominio; las invariantes se cumplieron al crearse.")]
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static AuditRecordAggregate RehydrateAuditRecord(AuditRecordRecord record)
    {
        var props = new AuditRecordProps(
            IdValueObject.Load(record.Id),
            record.WhoActed,
            DomainEnumerationMapper.FromValue<SubjectType>(record.SubjectTypeId),
            record.WhatChanged,
            record.EventType,
            DomainEnumerationMapper.FromValue<AuditResult>(record.AuditResultId),
            record.AffectedEntityId,
            record.AffectedEntityType,
            record.RootTenantId,
            record.Metadata);

        props.WhenOccurred = record.WhenOccurred;

        var aggregate = Construct<AuditRecordAggregate, AuditRecordProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();

        return aggregate;
    }

    private static TEntity Construct<TEntity, TProps>(TProps props)
        where TEntity : class
        where TProps : class
    {
        var ctor = typeof(TEntity).GetConstructor(InstanceFlags, null, [typeof(TProps)], null)
            ?? throw new InvalidOperationException($"Constructor for {typeof(TEntity).Name} not found.");

        return (TEntity)ctor.Invoke([props]);
    }
}
