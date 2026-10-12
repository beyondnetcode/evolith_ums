#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Kernel;
using UserDocumentAggregate = Evolith.Ums.Domain.Approvals.UserDocument.UserDocument;

public sealed class InMemoryUserDocumentRepository : IUserDocumentRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, UserDocumentAggregate> _store = new();
    public IUnitOfWork UnitOfWork => this;

    public Task<UserDocumentAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    { _store.TryGetValue(id, out var e); e?.BrokenRules.Clear(); return Task.FromResult(e); }

    public Task<UserDocumentAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<UserDocumentAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    { var all = _store.Values.ToList(); all.ForEach(e => e.BrokenRules.Clear()); return Task.FromResult<IReadOnlyList<UserDocumentAggregate>>(all); }

    public Task<IReadOnlyList<UserDocumentAggregate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    { var f = _store.Values.Where(e => e.Props.UserId.GetValue() == userId).ToList(); f.ForEach(e => e.BrokenRules.Clear()); return Task.FromResult<IReadOnlyList<UserDocumentAggregate>>(f); }

    public Task AddAsync(UserDocumentAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task UpdateAsync(UserDocumentAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void Seed(UserDocumentAggregate a)
    {
        a.DomainEvents.MarkChangesAsCommitted();
        _store[a.Props.Id.GetValue()] = a;
    }
    public void Dispose() { }
}

#pragma warning restore S4144
