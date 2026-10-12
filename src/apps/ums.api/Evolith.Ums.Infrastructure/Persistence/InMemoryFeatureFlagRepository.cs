#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Kernel;
using FeatureFlagAggregate = Evolith.Ums.Domain.Configuration.FeatureFlag.FeatureFlag;

public sealed class InMemoryFeatureFlagRepository : IFeatureFlagRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, FeatureFlagAggregate> _store = new();
    public IUnitOfWork UnitOfWork => this;

    public Task<FeatureFlagAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var entity);
        entity?.BrokenRules.Clear();
        return Task.FromResult(entity);
    }

    public Task<FeatureFlagAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<FeatureFlagAggregate?> GetByCodeAsync(string flagCode, CancellationToken cancellationToken = default)
    {
        var entity = _store.Values.FirstOrDefault(item =>
            string.Equals(item.Props.FlagCode, flagCode, StringComparison.OrdinalIgnoreCase));
        entity?.BrokenRules.Clear();
        return Task.FromResult(entity);
    }

    public Task<FeatureFlagAggregate?> GetBySystemSuiteAndCodeAsync(Guid systemSuiteId, string flagCode, CancellationToken cancellationToken = default)
    {
        var entity = _store.Values.FirstOrDefault(item =>
            item.Props.SystemSuiteId.GetValue() == systemSuiteId &&
            string.Equals(item.Props.FlagCode, flagCode, StringComparison.OrdinalIgnoreCase));
        entity?.BrokenRules.Clear();
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<FeatureFlagAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var items = tenantId.HasValue
            ? _store.Values.Where(item => item.Props.TenantId?.GetValue() == tenantId.Value).ToList()
            : _store.Values.ToList();
        items.ForEach(item => item.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<FeatureFlagAggregate>>(items);
    }

    public Task<IReadOnlyList<FeatureFlagAggregate>> GetBySystemSuiteIdAsync(Guid systemSuiteId, CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(item => item.Props.SystemSuiteId.GetValue() == systemSuiteId)
            .ToList();
        items.ForEach(item => item.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<FeatureFlagAggregate>>(items);
    }

    // En memoria no hay `Include` que evitar: la distinción sirve al lado PostgreSQL.
    public Task<IReadOnlyList<FeatureFlagAggregate>> GetBySystemSuiteIdForEvaluationAsync(Guid systemSuiteId, CancellationToken cancellationToken = default)
        => GetBySystemSuiteIdAsync(systemSuiteId, cancellationToken);

    public Task AddAsync(FeatureFlagAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FeatureFlagAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(FeatureFlagAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public void Dispose() { }
}

#pragma warning restore S4144
