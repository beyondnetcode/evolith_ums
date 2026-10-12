#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
using System.Collections.Concurrent;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.TenantSignupRequest;
using Evolith.Ums.Domain.Kernel;
using TenantSignupRequestAggregate = Evolith.Ums.Domain.Identity.TenantSignupRequest.TenantSignupRequest;

namespace Evolith.Ums.Infrastructure.Persistence.Identity;

public sealed class InMemoryTenantSignupRequestRepository : ITenantSignupRequestRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, TenantSignupRequestAggregate> _store = new();

    public IUnitOfWork UnitOfWork => this;

    public Task<TenantSignupRequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var request);
        request?.BrokenRules.Clear();
        return Task.FromResult(request);
    }

    public Task<IReadOnlyList<TenantSignupRequestAggregate>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TenantSignupRequestAggregate>>(_store.Values.OrderBy(x => x.CompanyName.GetValue()).ToList());

    public Task<IReadOnlyList<TenantSignupRequestAggregate>> GetPendingAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TenantSignupRequestAggregate>>(
            _store.Values.Where(x => x.Status == TenantSignupRequestStatus.Pending).OrderBy(x => x.CompanyName.GetValue()).ToList());

    public Task AddAsync(TenantSignupRequestAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.GetId().GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TenantSignupRequestAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.GetId().GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(TenantSignupRequestAggregate aggregate)
    {
        aggregate.BrokenRules.Clear();
        _store[aggregate.GetId().GetValue()] = aggregate;
    }

    public void Dispose() { }
}

#pragma warning restore S4144
