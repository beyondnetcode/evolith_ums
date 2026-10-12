#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.Kernel;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;

/// <summary>
/// Implementación en memoria de <see cref="IRolePromotionRequestRepository"/> (IGA, ADR-UMS-093) para
/// dev/tests sin PostgreSQL. Guarda los agregados por identificador y respeta el acotamiento por
/// inquilino y el filtrado por estado en las consultas. Mantiene la misma superficie que la variante
/// PostgreSQL.
/// </summary>
public sealed class InMemoryRolePromotionRequestRepository : IRolePromotionRequestRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, RolePromotionRequestAggregate> _store = new();
    public IUnitOfWork UnitOfWork => this;

    public Task<RolePromotionRequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    { _store.TryGetValue(id, out var e); e?.BrokenRules.Clear(); return Task.FromResult(e); }

    public Task<RolePromotionRequestAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<RolePromotionRequestAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(x => !tenantId.HasValue || x.TenantId.GetValue() == tenantId.Value)
            .ToList();
        items.ForEach(e => e.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<RolePromotionRequestAggregate>>(items);
    }

    public Task<IReadOnlyList<RolePromotionRequestAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = _store.Values.Where(x => x.TenantId.GetValue() == tenantId).ToList();
        items.ForEach(e => e.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<RolePromotionRequestAggregate>>(items);
    }

    public Task<IReadOnlyList<RolePromotionRequestAggregate>> GetByTenantAndStatusAsync(Guid tenantId, string status, CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(x => x.TenantId.GetValue() == tenantId && x.Status.Name == status)
            .ToList();
        items.ForEach(e => e.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<RolePromotionRequestAggregate>>(items);
    }

    public Task AddAsync(RolePromotionRequestAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task UpdateAsync(RolePromotionRequestAggregate a, CancellationToken cancellationToken = default) { _store[a.Props.Id.GetValue()] = a; return Task.CompletedTask; }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(RolePromotionRequestAggregate a)
    {
        a.DomainEvents.MarkChangesAsCommitted();
        _store[a.Props.Id.GetValue()] = a;
    }

    public void Dispose() { }
}

#pragma warning restore S4144
