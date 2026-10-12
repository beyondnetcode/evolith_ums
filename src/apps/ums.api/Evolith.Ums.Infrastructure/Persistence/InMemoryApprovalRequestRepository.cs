#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Kernel;
using ApprovalRequestAggregate = Evolith.Ums.Domain.Approvals.ApprovalRequest.ApprovalRequest;

public sealed class InMemoryApprovalRequestRepository : IApprovalRequestRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, ApprovalRequestAggregate> _store = new();
    public IUnitOfWork UnitOfWork => this;

    public Task<ApprovalRequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    { _store.TryGetValue(id, out var e); e?.BrokenRules.Clear(); return Task.FromResult(e); }

    public Task<ApprovalRequestAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<ApprovalRequestAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    { var all = _store.Values.ToList(); all.ForEach(e => e.BrokenRules.Clear()); return Task.FromResult<IReadOnlyList<ApprovalRequestAggregate>>(all); }

    public Task<IReadOnlyList<ApprovalRequestAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    { var f = _store.Values.ToList(); f.ForEach(e => e.BrokenRules.Clear()); return Task.FromResult<IReadOnlyList<ApprovalRequestAggregate>>(f); }

    public Task<bool> ExistsPendingForScopeAsync(Guid userId, Guid systemId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var exists = _store.Values.Any(x =>
            x.TargetUserId.GetValue() == userId &&
            x.RequestedSystemId.GetValue() == systemId &&
            x.RequestedBranchId?.GetValue() == branchId &&
            x.Status == ApprovalStatus.Pending);
        return Task.FromResult(exists);
    }

    public Task AddAsync(ApprovalRequestAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task UpdateAsync(ApprovalRequestAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void Seed(ApprovalRequestAggregate a)
    {
        a.DomainEvents.MarkChangesAsCommitted();
        _store[a.Props.Id.GetValue()] = a;
    }
    public void Dispose() { }
}

#pragma warning restore S4144
