using System;
using System.Reflection;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.IGA.RoleMaturityStatus;
using Evolith.Ums.Domain.IGA.RolePromotionRequest;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Iga.Entities;
using BeyondNetCode.Shell.Ddd.ValueObjects.Audit;

namespace Evolith.Ums.Infrastructure.Persistence.Reflection;

using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;

/// <summary>
/// Rehidrata los agregados del contexto acotado IGA (ADR-UMS-093) desde sus registros de persistencia.
/// Invoca el constructor de rehidratación privado por reflexión (mismo patrón que
/// <see cref="ApprovalsAggregateFactory"/>) y marca los eventos como confirmados para que la carga
/// no vuelva a emitir el evento de creación ni deje reglas rotas colgando.
/// </summary>
internal static class IgaAggregateFactory
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada para rehidratación de agregados. Reconstruir un agregado " +
                        "ya validado (ADR-UMS-069) desde persistencia sin exponer setters públicos preserva la pureza " +
                        "del dominio; las invariantes se cumplieron al crearse.")]
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static RoleMaturityStatusAggregate RehydrateRoleMaturityStatus(RoleMaturityStatusRecord record)
    {
        var props = new RoleMaturityStatusProps(
            RoleMaturityStatusId.Load(record.Id),
            TenantId.Load(record.TenantId),
            UserId.Load(record.UserId),
            RoleId.Load(record.RoleId),
            (RoleMaturityLevel)record.CurrentMaturityLevel,
            record.NextEligibleMaturityLevel.HasValue ? (RoleMaturityLevel)record.NextEligibleMaturityLevel.Value : null,
            record.AssignedAt,
            record.CurrentLevelSince,
            record.EligibleForPromotionAt,
            record.CompletedCertificationsCount,
            record.CompletedTrainingsCount,
            record.PerformanceScore,
            record.HasNoComplianceIssues,
            string.IsNullOrEmpty(record.BlockingFactor) ? null : TextValueObject.Create(record.BlockingFactor),
            record.LastReviewedAt,
            BuildAudit(record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan));

        var aggregate = Construct<RoleMaturityStatusAggregate, RoleMaturityStatusProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    public static RolePromotionRequestAggregate RehydrateRolePromotionRequest(RolePromotionRequestRecord record)
    {
        var props = new RolePromotionRequestProps(
            RolePromotionRequestId.Load(record.Id),
            TenantId.Load(record.TenantId),
            UserId.Load(record.TargetUserId),
            UserId.Load(record.RequesterId),
            RoleId.Load(record.CurrentRoleId),
            RoleId.Load(record.TargetRoleId),
            DomainEnumerationMapper.FromValue<RolePromotionStatus>(record.StatusId),
            record.RiskScore.HasValue ? RiskScore.Load(record.RiskScore.Value) : null,
            record.ApproverId.HasValue ? UserId.Load(record.ApproverId.Value) : null,
            record.SecurityReviewerId.HasValue ? UserId.Load(record.SecurityReviewerId.Value) : null,
            record.ExecutorId.HasValue ? UserId.Load(record.ExecutorId.Value) : null,
            record.VerifierId.HasValue ? UserId.Load(record.VerifierId.Value) : null,
            record.DecisionReason,
            BuildAudit(record.CreatedBy, record.CreatedAtUtc, record.UpdatedBy, record.UpdatedAtUtc, record.AuditTimeSpan));

        var aggregate = Construct<RolePromotionRequestAggregate, RolePromotionRequestProps>(props);
        aggregate.DomainEvents.MarkChangesAsCommitted();
        aggregate.BrokenRules.Clear();
        return aggregate;
    }

    private static AuditValueObject BuildAudit(string createdBy, DateTime createdAtUtc, string? updatedBy, DateTime? updatedAtUtc, string auditTimeSpan)
        => AuditValueObject.Load(new AuditProps
        {
            CreatedBy = createdBy,
            CreatedAt = createdAtUtc,
            UpdatedBy = updatedBy,
            UpdatedAt = updatedAtUtc,
            TimeSpan = auditTimeSpan,
        });

    private static TEntity Construct<TEntity, TProps>(TProps props)
        where TEntity : class
        where TProps : class
    {
        var ctor = typeof(TEntity).GetConstructor(InstanceFlags, null, [typeof(TProps)], null)
            ?? throw new InvalidOperationException($"Constructor for {typeof(TEntity).Name} not found.");

        return (TEntity)ctor.Invoke([props]);
    }
}
