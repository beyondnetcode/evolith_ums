namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using BeyondNetCode.Shell.Ddd.Interfaces;
using Evolith.Ums.Domain.Audit.AuditRecord;
using AuditRecordAggregate = Evolith.Ums.Domain.Audit.AuditRecord.AuditRecord;

public sealed class InMemoryAuditRecordRepository : IAuditRecordRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, AuditRecordAggregate> _store = new();

    public IUnitOfWork UnitOfWork => this;

    public Task<AuditRecordAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task AppendAsync(AuditRecordAggregate record, CancellationToken ct = default)
    {
        _store[record.Props.Id.GetValue()] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditRecordAggregate>> QueryByEntityAsync(Guid entityId, string entityType, Guid rootTenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var filtered = _store.Values
            .Where(r => r.Props.AffectedEntityId == entityId &&
                        r.Props.AffectedEntityType == entityType &&
                        r.Props.RootTenantId == rootTenantId &&
                        r.Props.WhenOccurred >= from &&
                        r.Props.WhenOccurred <= to)
            .ToList();
        return Task.FromResult<IReadOnlyList<AuditRecordAggregate>>(filtered);
    }

    public Task<IReadOnlyList<AuditRecordAggregate>> QueryByActorAsync(Guid actorId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var filtered = _store.Values
            .Where(r => r.Props.WhoActed == actorId &&
                        r.Props.WhenOccurred >= from &&
                        r.Props.WhenOccurred <= to)
            .ToList();
        return Task.FromResult<IReadOnlyList<AuditRecordAggregate>>(filtered);
    }

    public Task<IReadOnlyList<AuditRecordAggregate>> QueryByEventTypeAsync(string eventType, Guid rootTenantId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var query = _store.Values
            .Where(r => r.Props.RootTenantId == rootTenantId &&
                        r.Props.WhenOccurred >= from &&
                        r.Props.WhenOccurred <= to);

        if (!string.Equals(eventType, "*", StringComparison.Ordinal))
        {
            query = query.Where(r => r.Props.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<AuditRecordAggregate>>(query.ToList());
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(object entity, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(AuditRecordAggregate record)
    {
        record.DomainEvents.MarkChangesAsCommitted();
        _store[record.Props.Id.GetValue()] = record;
    }
    public void Dispose() { }
}
