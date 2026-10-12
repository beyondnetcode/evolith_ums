#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.Kernel;
using RoleMaturityStatusAggregate = Evolith.Ums.Domain.IGA.RoleMaturityStatus.RoleMaturityStatus;

/// <summary>
/// Implementación en memoria de <see cref="IRoleMaturityStatusRepository"/> (IGA, ADR-UMS-093) para
/// dev/tests sin PostgreSQL. Guarda los agregados por identificador y respeta el acotamiento por
/// inquilino en las consultas. Mantiene la misma superficie que la variante PostgreSQL.
/// </summary>
public sealed class InMemoryRoleMaturityStatusRepository : IRoleMaturityStatusRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, RoleMaturityStatusAggregate> _store = new();
    public IUnitOfWork UnitOfWork => this;

    public Task<RoleMaturityStatusAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    { _store.TryGetValue(id, out var e); e?.BrokenRules.Clear(); return Task.FromResult(e); }

    public Task<RoleMaturityStatusAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<RoleMaturityStatusAggregate>> GetByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(x => x.TenantId.GetValue() == tenantId && x.UserId.GetValue() == userId)
            .ToList();
        items.ForEach(e => e.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<RoleMaturityStatusAggregate>>(items);
    }

    public Task<RoleMaturityStatusAggregate?> GetByUserAndRoleAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var item = _store.Values.FirstOrDefault(x =>
            x.TenantId.GetValue() == tenantId &&
            x.UserId.GetValue() == userId &&
            x.RoleId.GetValue() == roleId);
        item?.BrokenRules.Clear();
        return Task.FromResult(item);
    }

    public Task<IReadOnlyList<RoleMaturityStatusAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(x => !tenantId.HasValue || x.TenantId.GetValue() == tenantId.Value)
            .ToList();
        items.ForEach(e => e.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<RoleMaturityStatusAggregate>>(items);
    }

    public Task AddAsync(RoleMaturityStatusAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task UpdateAsync(RoleMaturityStatusAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(RoleMaturityStatusAggregate a)
    {
        a.DomainEvents.MarkChangesAsCommitted();
        _store[a.Props.Id.GetValue()] = a;
    }

    public void Dispose() { }
}

#pragma warning restore S4144
