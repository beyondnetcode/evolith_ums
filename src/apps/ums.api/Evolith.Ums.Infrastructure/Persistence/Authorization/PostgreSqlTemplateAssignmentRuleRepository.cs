using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization;

using AssignmentRuleAggregate = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule;

public sealed class PostgreSqlTemplateAssignmentRuleRepository(UmsPlatformDbContext dbContext)
    : ITemplateAssignmentRuleRepository, IUnitOfWork
{
    private readonly HashSet<AssignmentRuleAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<AssignmentRuleAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.TemplateAssignmentRules
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<AssignmentRuleAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<AssignmentRuleAggregate>> GetByTenantIdAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.TemplateAssignmentRules
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.Priority)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<AssignmentRuleAggregate>> GetActiveByTenantAndRoleAsync(
        Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var activeStatusId = (int)Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRuleStatus.Active;

        var records = await dbContext.TemplateAssignmentRules
            .Where(x => x.TenantId == tenantId && x.RoleId == roleId && x.StatusId == activeStatusId)
            .OrderByDescending(x => x.Priority)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task<bool> ExistsActiveWithPriorityAsync(
        Guid tenantId, int priority, CancellationToken cancellationToken = default)
    {
        var activeStatusId = (int)Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRuleStatus.Active;

        return dbContext.TemplateAssignmentRules
            .AnyAsync(x => x.TenantId == tenantId && x.Priority == priority && x.StatusId == activeStatusId,
                cancellationToken);
    }

    public Task AddAsync(AssignmentRuleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.TemplateAssignmentRules.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(AssignmentRuleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.TemplateAssignmentRules
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"TemplateAssignmentRule {aggregate.Props.Id.GetValue()} does not exist.");

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

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var aggregate in _trackedAggregates)
        {
            aggregate.DomainEvents.MarkChangesAsCommitted();
        }

        _trackedAggregates.Clear();
        return true;
    }

    public void Dispose() { }

    private static AssignmentRuleAggregate Rehydrate(TemplateAssignmentRuleRecord record)
        => AuthorizationAggregateFactory.RehydrateAssignmentRule(record);

    private static TemplateAssignmentRuleRecord ToRecord(AssignmentRuleAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new TemplateAssignmentRuleRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            TemplateId = aggregate.Props.TemplateId.GetValue(),
            RoleId = aggregate.Props.RoleId.GetValue(),
            Priority = aggregate.Priority,
            StatusId = (int)aggregate.Status,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan,
        };
    }

    private static void Apply(TemplateAssignmentRuleRecord target, AssignmentRuleAggregate source)
    {
        var replacement = ToRecord(source);
        target.TenantId = replacement.TenantId;
        target.TemplateId = replacement.TemplateId;
        target.RoleId = replacement.RoleId;
        target.Priority = replacement.Priority;
        target.StatusId = replacement.StatusId;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
