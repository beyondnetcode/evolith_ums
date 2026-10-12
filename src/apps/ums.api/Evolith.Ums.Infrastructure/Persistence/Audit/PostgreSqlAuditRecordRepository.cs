using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeyondNetCode.Shell.Ddd.Interfaces;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Infrastructure.Persistence.Audit.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Audit;

using AuditRecordAggregate = Evolith.Ums.Domain.Audit.AuditRecord.AuditRecord;

public sealed class PostgreSqlAuditRecordRepository : IAuditRecordRepository, BeyondNetCode.Shell.Ddd.Interfaces.IUnitOfWork
{
    private readonly UmsPlatformDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly HashSet<AuditRecordAggregate> _trackedAggregates = [];

    public PostgreSqlAuditRecordRepository(UmsPlatformDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    BeyondNetCode.Shell.Ddd.Interfaces.IUnitOfWork IRepository<AuditRecord>.UnitOfWork => this;

    public async Task<AuditRecordAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // G-103 — Aislamiento por inquilino en la lectura por Id. Sin este filtro, un actor podía
        // leer la traza de OTRO inquilino conociendo su Id (fuga cross-tenant). La tabla de
        // auditoría NO lleva query filter global de EF a propósito: rompería la lectura cross-tenant
        // legítima del admin de plataforma (GetAllAuditRecordsQueryHandler fija
        // effectiveTenantId = IsInternalAdmin ? request.TenantId : OrganizationId, que para el admin
        // difiere de OrganizationId) y las QueryBy* que reciben el inquilino de forma explícita. Por
        // eso el aislamiento se aplica aquí, en la capa de aplicación, igual que hacen las QueryBy*.
        //
        // Bypass legítimo (misma semántica que effectiveTenantId del handler y el convenio FIX-05):
        //   - IsInternalAdmin  → admin de plataforma con vista cross-tenant.
        //   - OrganizationId nulo → contexto de sistema/segundo plano (sembrado, outbox).
        // En cualquier otro caso se restringe a la traza del inquilino del contexto.
        var query = _dbContext.Set<AuditRecordRecord>().Where(x => x.Id == id);

        var organizationId = _tenantContext.OrganizationId;
        if (!_tenantContext.IsInternalAdmin && organizationId.HasValue)
        {
            query = query.Where(x => x.RootTenantId == organizationId.Value);
        }

        var record = await query.FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task AppendAsync(AuditRecordAggregate record, CancellationToken ct = default)
    {
        _dbContext.Set<AuditRecordRecord>().Add(ToRecord(record));
        _trackedAggregates.Add(record);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<AuditRecordAggregate>> QueryByEntityAsync(
        Guid entityId, string entityType, Guid rootTenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var records = await _dbContext.Set<AuditRecordRecord>()
            .Where(r => r.AffectedEntityId == entityId &&
                        r.AffectedEntityType == entityType &&
                        r.RootTenantId == rootTenantId &&
                        r.WhenOccurred >= from &&
                        r.WhenOccurred <= to)
            .OrderByDescending(r => r.WhenOccurred)
            .ToListAsync(ct);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<AuditRecordAggregate>> QueryByActorAsync(
        Guid actorId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var records = await _dbContext.Set<AuditRecordRecord>()
            .Where(r => r.WhoActed == actorId &&
                        r.WhenOccurred >= from &&
                        r.WhenOccurred <= to)
            .OrderByDescending(r => r.WhenOccurred)
            .ToListAsync(ct);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<AuditRecordAggregate>> QueryByEventTypeAsync(
        string eventType, Guid rootTenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var query = _dbContext.Set<AuditRecordRecord>()
            .Where(r => r.RootTenantId == rootTenantId &&
                        r.WhenOccurred >= from &&
                        r.WhenOccurred <= to);

        if (!string.Equals(eventType, "*", StringComparison.Ordinal))
        {
            query = query.Where(r => r.EventType == eventType);
        }

        var records = await query.OrderByDescending(r => r.WhenOccurred).ToListAsync(ct);
        return records.Select(Rehydrate).ToList();
    }

    // IUnitOfWork Implementation
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveEntitiesAsync(object entity, CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in _trackedAggregates)
        {
            aggregate.DomainEvents.MarkChangesAsCommitted();
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        _trackedAggregates.Clear();
        return true;
    }

    public void Dispose()
    {
        // Managed by EF Core lifecycle
    }

    private static AuditRecordAggregate Rehydrate(AuditRecordRecord record)
        => AuditAggregateFactory.RehydrateAuditRecord(record);

    private static AuditRecordRecord ToRecord(AuditRecordAggregate aggregate)
    {
        return new AuditRecordRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            WhoActed = aggregate.WhoActed,
            SubjectTypeId = aggregate.SubjectType.Id,
            WhenOccurred = aggregate.WhenOccurred,
            WhatChanged = aggregate.WhatChanged,
            EventType = aggregate.EventType,
            AuditResultId = aggregate.AuditResult.Id,
            AffectedEntityId = aggregate.AffectedEntityId,
            AffectedEntityType = aggregate.AffectedEntityType,
            RootTenantId = aggregate.RootTenantId,
            Metadata = aggregate.Metadata
        };
    }
}
