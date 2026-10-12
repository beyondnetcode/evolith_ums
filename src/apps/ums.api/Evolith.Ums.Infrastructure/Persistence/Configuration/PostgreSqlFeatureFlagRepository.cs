using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Configuration;

using FeatureFlagAggregate = Evolith.Ums.Domain.Configuration.FeatureFlag.FeatureFlag;

public sealed class PostgreSqlFeatureFlagRepository(UmsPlatformDbContext dbContext) : IFeatureFlagRepository, IUnitOfWork
{
    private readonly HashSet<FeatureFlagAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<FeatureFlagAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.FeatureFlags
            .AsSplitQuery()
            .Include(x => x.EvaluationLogs)
            .Include(x => x.Criteria)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<FeatureFlagAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<FeatureFlagAggregate?> GetByCodeAsync(string flagCode, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.FeatureFlags
            .AsSplitQuery()
            .Include(x => x.EvaluationLogs)
            .Include(x => x.Criteria)
            .FirstOrDefaultAsync(x => x.FlagCode == flagCode, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<FeatureFlagAggregate?> GetBySystemSuiteAndCodeAsync(Guid systemSuiteId, string flagCode, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.FeatureFlags
            .AsSplitQuery()
            .Include(x => x.EvaluationLogs)
            .Include(x => x.Criteria)
            .FirstOrDefaultAsync(x => x.SystemSuiteId == systemSuiteId && x.FlagCode == flagCode, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public async Task<IReadOnlyList<FeatureFlagAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<FeatureFlagRecord> query = dbContext.FeatureFlags.AsSplitQuery()
            .Include(x => x.EvaluationLogs)
            .Include(x => x.Criteria);

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.OrderBy(x => x.FlagCode).ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<FeatureFlagAggregate>> GetBySystemSuiteIdForEvaluationAsync(Guid systemSuiteId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.FeatureFlags
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Criteria)
            .Where(x => x.SystemSuiteId == systemSuiteId)
            .OrderBy(x => x.FlagCode)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<FeatureFlagAggregate>> GetBySystemSuiteIdAsync(Guid systemSuiteId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.FeatureFlags
            .AsSplitQuery()
            .Include(x => x.EvaluationLogs)
            .Include(x => x.Criteria)
            .Where(x => x.SystemSuiteId == systemSuiteId)
            .OrderBy(x => x.FlagCode)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(FeatureFlagAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.FeatureFlags.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(FeatureFlagAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.FeatureFlags
            .Include(x => x.EvaluationLogs)
            .Include(x => x.Criteria)
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"FeatureFlag {aggregate.Props.Id.GetValue()} does not exist.");

        Apply(existing, aggregate);
        _trackedAggregates.Add(aggregate);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in _trackedAggregates)
        {
            await dbContext.PublishDomainEventsAsync(aggregate.DomainEvents.GetUncommittedChanges(), cancellationToken);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            // FIX-03: surface optimistic concurrency failures as 409 Conflict
            var entry = ex.Entries.FirstOrDefault();
            var id = (Guid)(entry?.Property("Id").CurrentValue ?? Guid.Empty);
            throw new ConcurrencyConflictException(entry?.Metadata.Name ?? "Unknown", id);
        }

        foreach (var aggregate in _trackedAggregates)
        {
            aggregate.DomainEvents.MarkChangesAsCommitted();
        }

        _trackedAggregates.Clear();
        return true;
    }

    public void Dispose() => dbContext.Dispose();

    private static FeatureFlagAggregate Rehydrate(FeatureFlagRecord record)
        => ConfigurationAggregateFactory.RehydrateFeatureFlag(record, record.EvaluationLogs, record.Criteria);

    private static FeatureFlagRecord ToRecord(FeatureFlagAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new FeatureFlagRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            SystemSuiteId = aggregate.Props.SystemSuiteId.GetValue(),
            TenantId = aggregate.Props.TenantId?.GetValue(),
            FlagCode = aggregate.Props.FlagCode,
            FlagTypeId = aggregate.Props.FlagType.Id,
            FlagTargets = aggregate.Props.FlagTargets,
            StatusId = aggregate.Props.Status.Id,
            LinkedResourceTypeId = aggregate.Props.LinkedResourceType?.Id,
            LinkedResourceId = aggregate.Props.LinkedResourceId?.GetValue(),
            RolloutPercentage = aggregate.Props.RolloutPercentage,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
            EvaluationLogs = aggregate.EvaluationLog.Select(log => new FeatureFlagEvaluationLogRecord
            {
                Id = log.Props.Id.GetValue(),
                FeatureFlagId = aggregate.Props.Id.GetValue(),
                EvaluatedBy = log.Props.EvaluatedBy,
                Result = log.Props.Result,
                Context = log.Props.Context,
                EvaluatedAtUtc = log.Props.EvaluatedAt,
            }).ToList(),
            Criteria = aggregate.Criteria.Select(c => new FeatureFlagCriteriaRecord
            {
                Id = c.Props.Id.GetValue(),
                FeatureFlagId = aggregate.Props.Id.GetValue(),
                CriteriaType = c.CriteriaType,
                Operator = c.Operator,
                Value = c.Value,
                CreatedAtUtc = c.CreatedAtUtc,
            }).ToList(),
        };
    }

    private void Apply(FeatureFlagRecord target, FeatureFlagAggregate source)
    {
        var replacement = ToRecord(source);

        target.SystemSuiteId = replacement.SystemSuiteId;
        target.TenantId = replacement.TenantId;
        target.FlagCode = replacement.FlagCode;
        target.FlagTypeId = replacement.FlagTypeId;
        target.FlagTargets = replacement.FlagTargets;
        target.StatusId = replacement.StatusId;
        target.LinkedResourceTypeId = replacement.LinkedResourceTypeId;
        target.LinkedResourceId = replacement.LinkedResourceId;
        target.RolloutPercentage = replacement.RolloutPercentage;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;

        EfChildCollectionReconciler.ReconcileById(
            dbContext,
            target.EvaluationLogs,
            replacement.EvaluationLogs,
            log => log.Id,
            UpdateEvaluationLog);

        EfChildCollectionReconciler.ReconcileById(
            dbContext,
            target.Criteria,
            replacement.Criteria,
            criterion => criterion.Id,
            UpdateCriterion);
    }

    private static void UpdateEvaluationLog(FeatureFlagEvaluationLogRecord target, FeatureFlagEvaluationLogRecord source)
    {
        target.FeatureFlagId = source.FeatureFlagId;
        target.EvaluatedBy = source.EvaluatedBy;
        target.Result = source.Result;
        target.Context = source.Context;
        target.EvaluatedAtUtc = source.EvaluatedAtUtc;
    }

    private static void UpdateCriterion(FeatureFlagCriteriaRecord target, FeatureFlagCriteriaRecord source)
    {
        target.FeatureFlagId = source.FeatureFlagId;
        target.CriteriaType = source.CriteriaType;
        target.Operator = source.Operator;
        target.Value = source.Value;
        target.CreatedAtUtc = source.CreatedAtUtc;
    }
}
