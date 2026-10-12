using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Infrastructure.Persistence.Approvals.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;

namespace Evolith.Ums.Infrastructure.Persistence.Approvals;

using NotificationRuleAggregate = Evolith.Ums.Domain.Approvals.NotificationRule.NotificationRule;

public sealed class PostgreSqlNotificationRuleRepository : INotificationRuleRepository, IUnitOfWork
{
    private readonly UmsPlatformDbContext _dbContext;
    private readonly HashSet<NotificationRuleAggregate> _trackedAggregates = [];

    public PostgreSqlNotificationRuleRepository(UmsPlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IUnitOfWork UnitOfWork => this;

    public async Task<NotificationRuleAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.Set<NotificationRuleRecord>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record is null ? null : Rehydrate(record);
    }

    public Task<NotificationRuleAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<NotificationRuleAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        IQueryable<NotificationRuleRecord> query = _dbContext.Set<NotificationRuleRecord>();

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var records = await query.ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public async Task<IReadOnlyList<NotificationRuleAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.Set<NotificationRuleRecord>()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return records.Select(Rehydrate).ToList();
    }

    public Task<bool> ExistsDuplicateAsync(Guid tenantId, string channel, string recipient, CancellationToken cancellationToken = default)
    {
        NotificationChannel? channelEnum;
        try { channelEnum = DomainEnumerationMapper.FromName<NotificationChannel>(channel); }
        catch { return Task.FromResult(false); }

        return _dbContext.Set<NotificationRuleRecord>()
            .AnyAsync(x => x.TenantId == tenantId
                        && x.ChannelId == channelEnum.Id
                        && x.Recipient == recipient
                        && x.IsActive,
                cancellationToken);
    }

    public Task AddAsync(NotificationRuleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _dbContext.Set<NotificationRuleRecord>().Add(ToRecord(aggregate));
        _trackedAggregates.Add(aggregate);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(NotificationRuleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Set<NotificationRuleRecord>()
            .FirstOrDefaultAsync(x => x.Id == aggregate.Props.Id.GetValue(), cancellationToken)
            ?? throw new InvalidOperationException($"Notification rule {aggregate.Props.Id.GetValue()} does not exist.");

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

    public void Dispose()
    {
    }

    private static NotificationRuleAggregate Rehydrate(NotificationRuleRecord record)
        => ApprovalsAggregateFactory.RehydrateRule(record);

    private static NotificationRuleRecord ToRecord(NotificationRuleAggregate aggregate)
    {
        var audit = aggregate.Props.Audit.GetValue();
        return new NotificationRuleRecord
        {
            Id = aggregate.Props.Id.GetValue(),
            TenantId = aggregate.Props.TenantId.GetValue(),
            ChannelId = aggregate.Channel.Id,
            Recipient = aggregate.Recipient.GetValue(),
            IsActive = aggregate.IsActive,
            CreatedBy = audit.CreatedBy,
            CreatedAtUtc = audit.CreatedAt,
            UpdatedBy = audit.UpdatedBy,
            UpdatedAtUtc = audit.UpdatedAt,
            AuditTimeSpan = audit.TimeSpan
        };
    }

    private static void Apply(NotificationRuleRecord target, NotificationRuleAggregate source)
    {
        var replacement = ToRecord(source);

        target.TenantId = replacement.TenantId;
        target.ChannelId = replacement.ChannelId;
        target.Recipient = replacement.Recipient;
        target.IsActive = replacement.IsActive;
        target.CreatedBy = replacement.CreatedBy;
        target.CreatedAtUtc = replacement.CreatedAtUtc;
        target.UpdatedBy = replacement.UpdatedBy;
        target.UpdatedAtUtc = replacement.UpdatedAtUtc;
        target.AuditTimeSpan = replacement.AuditTimeSpan;
    }
}
