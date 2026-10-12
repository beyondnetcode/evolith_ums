using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization;

using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;

public sealed class PostgreSqlRoleRepository(UmsPlatformDbContext dbContext) : IRoleRepository, IUnitOfWork
{
    private readonly HashSet<RoleAggregate> _trackedAggregates = [];

    public IUnitOfWork UnitOfWork => this;

    public async Task<RoleAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Roles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return record is null ? null : AuthorizationAggregateFactory.RehydrateRole(record);
    }

    public async Task<RoleAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.Roles.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        return record is null ? null : AuthorizationAggregateFactory.RehydrateRole(record);
    }

    public async Task<RoleAggregate?> GetByCodeAsync(Guid systemSuiteId, Code code, CancellationToken cancellationToken = default)
    {
        var codeValue = code.GetValue();
        var record = await dbContext.Roles.FirstOrDefaultAsync(
            x => x.SystemSuiteId == systemSuiteId && x.Code == codeValue,
            cancellationToken);
        return record is null ? null : AuthorizationAggregateFactory.RehydrateRole(record);
    }

    public async Task<IReadOnlyList<RoleAggregate>> GetBySystemSuiteIdAsync(Guid systemSuiteId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.Roles
            .Where(x => x.SystemSuiteId == systemSuiteId)
            .OrderBy(x => x.HierarchyLevel)
            .ThenBy(x => x.PromotionOrder)
            .ThenBy(x => x.Value)
            .ToListAsync(cancellationToken);
        return records.Select(AuthorizationAggregateFactory.RehydrateRole).ToList();
    }

    public async Task<IReadOnlyList<RoleAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.Roles
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Value)
            .ToListAsync(cancellationToken);
        return records.Select(AuthorizationAggregateFactory.RehydrateRole).ToList();
    }

    public async Task<IReadOnlyList<RoleAggregate>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        var records = await dbContext.Roles
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return records.Select(AuthorizationAggregateFactory.RehydrateRole).ToList();
    }

    public Task AddAsync(RoleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        dbContext.Roles.Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(RoleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var target = await dbContext.Roles.FirstOrDefaultAsync(
            x => x.Id == aggregate.Props.Id.GetValue(),
            cancellationToken)
            ?? throw new InvalidOperationException($"Role {aggregate.Props.Id.GetValue()} does not exist.");
        Apply(target, aggregate);
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

    public void Dispose() => dbContext.Dispose();

    // ── Dependency guard queries ────────────────────────────────────────────

    public Task<int> CountActiveChildRolesAsync(Guid parentRoleId, CancellationToken cancellationToken = default)
        => dbContext.Roles.CountAsync(r => r.ParentRoleId == parentRoleId && r.IsActive, cancellationToken);

    private static RoleRecord ToRecord(RoleAggregate aggregate)
    {
        var props = aggregate.Props;
        if (props == null) throw new InvalidOperationException("Role aggregate has null Props");

        var audit = props.Audit?.GetValue();
        var now = DateTime.UtcNow;

        return new RoleRecord
        {
            Id = props.Id?.GetValue() ?? Guid.Empty,
            TenantId = aggregate.TenantId?.GetValue() ?? Guid.Empty,
            SystemSuiteId = aggregate.SystemSuiteId?.GetValue() ?? Guid.Empty,
            ParentRoleId = aggregate.ParentRoleId?.GetValue(),
            Code = aggregate.Code?.GetValue() ?? string.Empty,
            Value = aggregate.Value?.GetValue() ?? string.Empty,
            Description = aggregate.Description?.GetValue() ?? string.Empty,
            HierarchyLevel = aggregate.HierarchyLevel,
            PromotionOrder = aggregate.PromotionOrder,
            IsActive = aggregate.IsActive,
            CreatedBy = audit?.CreatedBy ?? "system",
            CreatedAtUtc = audit?.CreatedAt ?? now,
            UpdatedBy = audit?.UpdatedBy ?? "system",
            UpdatedAtUtc = audit?.UpdatedAt ?? now,
            AuditTimeSpan = audit?.TimeSpan ?? "00:00:00",
        };
    }

    private static void Apply(RoleRecord target, RoleAggregate aggregate)
    {
        var source = ToRecord(aggregate);
        target.ParentRoleId = source.ParentRoleId;
        target.Value = source.Value;
        target.Description = source.Description;
        target.HierarchyLevel = source.HierarchyLevel;
        target.PromotionOrder = source.PromotionOrder;
        target.IsActive = source.IsActive;
        target.UpdatedBy = source.UpdatedBy;
        target.UpdatedAtUtc = source.UpdatedAtUtc;
        target.AuditTimeSpan = source.AuditTimeSpan;
    }
}
