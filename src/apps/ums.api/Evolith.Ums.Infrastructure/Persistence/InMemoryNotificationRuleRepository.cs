#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Kernel;
using NotificationRuleAggregate = Evolith.Ums.Domain.Approvals.NotificationRule.NotificationRule;

public sealed class InMemoryNotificationRuleRepository : INotificationRuleRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, NotificationRuleAggregate> _store = new();
    public IUnitOfWork UnitOfWork => this;

    public Task<NotificationRuleAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    { _store.TryGetValue(id, out var e); e?.BrokenRules.Clear(); return Task.FromResult(e); }

    public Task<NotificationRuleAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<NotificationRuleAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    { var all = tenantId.HasValue ? _store.Values.Where(e => e.Props.TenantId.GetValue() == tenantId.Value).ToList() : _store.Values.ToList(); all.ForEach(e => e.BrokenRules.Clear()); return Task.FromResult<IReadOnlyList<NotificationRuleAggregate>>(all); }

    public Task<IReadOnlyList<NotificationRuleAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    { var f = _store.Values.Where(e => e.Props.TenantId.GetValue() == tenantId).ToList(); f.ForEach(e => e.BrokenRules.Clear()); return Task.FromResult<IReadOnlyList<NotificationRuleAggregate>>(f); }

    public Task<bool> ExistsDuplicateAsync(Guid tenantId, string channel, string recipient, CancellationToken cancellationToken = default)
    {
        var exists = _store.Values.Any(r =>
            r.Props.TenantId.GetValue() == tenantId &&
            r.Channel.Name.Equals(channel, StringComparison.OrdinalIgnoreCase) &&
            r.Recipient.GetValue().Equals(recipient, StringComparison.OrdinalIgnoreCase) &&
            r.IsActive);
        return Task.FromResult(exists);
    }

    public Task AddAsync(NotificationRuleAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task UpdateAsync(NotificationRuleAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void Seed(NotificationRuleAggregate a)
    {
        a.DomainEvents.MarkChangesAsCommitted();
        _store[a.Props.Id.GetValue()] = a;
    }
    public void Dispose() { }
}

#pragma warning restore S4144
