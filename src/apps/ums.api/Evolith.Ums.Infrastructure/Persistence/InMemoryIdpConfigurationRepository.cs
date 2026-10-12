#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Kernel;
using IdpConfigurationAggregate = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration;

public sealed class InMemoryIdpConfigurationRepository : IIdpConfigurationRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, IdpConfigurationAggregate> _store = new();
    public IUnitOfWork UnitOfWork => this;

    public Task<IdpConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var entity);
        entity?.BrokenRules.Clear();
        return Task.FromResult(entity);
    }

    public Task<IdpConfigurationAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<IdpConfigurationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var items = tenantId.HasValue
            ? _store.Values.Where(item => item.Props.TenantId.GetValue() == tenantId.Value).ToList()
            : _store.Values.ToList();
        items.ForEach(item => item.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<IdpConfigurationAggregate>>(items);
    }

    public Task<IReadOnlyList<IdpConfigurationAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = _store.Values.Where(item => item.Props.TenantId.GetValue() == tenantId).ToList();
        items.ForEach(item => item.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<IdpConfigurationAggregate>>(items);
    }

    public Task AddAsync(IdpConfigurationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(IdpConfigurationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(IdpConfigurationAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public void Dispose() { }
}

#pragma warning restore S4144
