#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.AssignmentRule;
using Evolith.Ums.Domain.Kernel;

using AssignmentRuleAggregate = Evolith.Ums.Domain.Authorization.AssignmentRule.TemplateAssignmentRule;

public sealed class InMemoryTemplateAssignmentRuleRepository : ITemplateAssignmentRuleRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, AssignmentRuleAggregate> _store = new();

    public IUnitOfWork UnitOfWork => this;

    public Task<AssignmentRuleAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var rule);
        rule?.BrokenRules.Clear();
        return Task.FromResult(rule);
    }

    public Task<AssignmentRuleAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<AssignmentRuleAggregate>> GetByTenantIdAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var all = _store.Values
            .Where(r => r.TenantId.GetValue() == tenantId)
            .OrderByDescending(r => r.Priority)
            .ToList();
        all.ForEach(r => r.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<AssignmentRuleAggregate>>(all);
    }

    public Task<IReadOnlyList<AssignmentRuleAggregate>> GetActiveByTenantAndRoleAsync(
        Guid tenantId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var matched = _store.Values
            .Where(r => r.TenantId.GetValue() == tenantId
                     && r.RoleId.GetValue() == roleId
                     && r.Status == TemplateAssignmentRuleStatus.Active)
            .OrderByDescending(r => r.Priority)
            .ToList();
        matched.ForEach(r => r.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<AssignmentRuleAggregate>>(matched);
    }

    public Task<bool> ExistsActiveWithPriorityAsync(
        Guid tenantId, int priority, CancellationToken cancellationToken = default)
    {
        var exists = _store.Values.Any(r =>
            r.TenantId.GetValue() == tenantId &&
            r.Priority == priority &&
            r.Status == TemplateAssignmentRuleStatus.Active);
        return Task.FromResult(exists);
    }

    public Task AddAsync(AssignmentRuleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AssignmentRuleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void Dispose() { }
}

#pragma warning restore S4144
