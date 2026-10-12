using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals;

using AccessEnforcementPolicyAggregate = Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy.AccessEnforcementPolicy;

public sealed class PostgreSqlAccessEnforcementPolicyRepository : IAccessEnforcementPolicyRepository, IUnitOfWork
{
    private readonly UmsPlatformDbContext _dbContext;
    private readonly HashSet<AccessEnforcementPolicyAggregate> _trackedAggregates = [];

    public PostgreSqlAccessEnforcementPolicyRepository(UmsPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IUnitOfWork UnitOfWork => this;

    public async Task<AccessEnforcementPolicyAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.Set<AccessEnforcementPolicyRecord>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<AccessEnforcementPolicyAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<AccessEnforcementPolicyAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<AccessEnforcementPolicyRecord> query = _dbContext.Set<AccessEnforcementPolicyRecord>();

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<AccessEnforcementPolicyAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.Set<AccessEnforcementPolicyRecord>()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task AddAsync(AccessEnforcementPolicyAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<AccessEnforcementPolicyRecord>().Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(AccessEnforcementPolicyAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Set<AccessEnforcementPolicyRecord>()
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Access enforcement policy {aggregate.Props.Id.GetValue()} does not exist.");

        Apply(existing, aggregate);
        _trackedAggregates.Add(aggregate);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var aggregate in _trackedAggregates)
        {
            await _dbContext.PublishDomainEventsAsync(aggregate.DomainEvents.GetUncommittedChanges(), cancellationToken);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
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

    public void Dispose() { }

    private static AccessEnforcementPolicyAggregate Rehydrate(AccessEnforcementPolicyRecord record)
        => ApprovalsAggregateFactory.RehydrateAccessEnforcementPolicy(record);

    private static AccessEnforcementPolicyRecord ToRecord(AccessEnforcementPolicyAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new AccessEnforcementPolicyRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            ProfileId = aggregate.ProfileId?.GetValue(),
            RoleId = aggregate.RoleId?.GetValue(),
            EnforcementActionId = aggregate.EnforcementAction.Id,
            IsActive = aggregate.IsActive,
            GracePeriodDays = aggregate.GracePeriodDays,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan
        };
    }

    private static void Apply(AccessEnforcementPolicyRecord target, AccessEnforcementPolicyAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.ProfileId = replacement.ProfileId;
        target.RoleId = replacement.RoleId;
        target.EnforcementActionId = replacement.EnforcementActionId;
        target.IsActive = replacement.IsActive;
        target.GracePeriodDays = replacement.GracePeriodDays;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
