#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;

public sealed class InMemoryProfileRepository : IProfileRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, ProfileAggregate> _store = new();
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public InMemoryProfileRepository(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // REC-05: Tenant filter for dev/test isolation
    private Guid? CurrentTenantId =>
        _httpContextAccessor?.HttpContext?.RequestServices
            .GetService<ITenantContext>()?.OrganizationId;

    public IUnitOfWork UnitOfWork => this;

    public Task<ProfileAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var profile);
        profile?.BrokenRules.Clear();
        return Task.FromResult(profile);
    }

    public Task<ProfileAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<ProfileAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var tid = tenantId ?? CurrentTenantId;
        var all = (tid.HasValue
            ? _store.Values.Where(p => p.Props.TenantId.GetValue() == tid.Value)
            : _store.Values).ToList();
        all.ForEach(p => p.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<ProfileAggregate>>(all);
    }

    public Task<IReadOnlyList<ProfileAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var filtered = _store.Values.Where(p => p.Props.TenantId.GetValue() == tenantId).ToList();
        filtered.ForEach(p => p.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<ProfileAggregate>>(filtered);
    }

    public Task<IReadOnlyList<ProfileAggregate>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var filtered = _store.Values.Where(p => p.Props.UserId.GetValue() == userId).ToList();
        filtered.ForEach(p => p.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<ProfileAggregate>>(filtered);
    }

    public Task<IReadOnlyList<ProfileAggregate>> GetActiveByUserAndTenantAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(x => x.Props.UserId.GetValue() == userId
                     && x.Props.TenantId.GetValue() == tenantId
                     && x.IsActive)
            .ToList();
        items.ForEach(item => item.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<ProfileAggregate>>(items);
    }

    public Task AddAsync(ProfileAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProfileAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(ProfileAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public void Dispose() { }

    // ── Dependency guard queries ────────────────────────────────────────────

    public Task<int> CountActiveByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Count(p => p.Props.RoleId.GetValue() == roleId && p.IsActive));

    public Task<int> CountActiveByTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
        => Task.FromResult(0); // InMemory: permissions are not stored separately

    public Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Count(p => p.Props.UserId.GetValue() == userId && p.IsActive));

    // ADR-0164 §2.2: guarda de cascada del cierre de sucursal.
    public Task<int> CountActiveByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Count(p => p.Props.BranchId?.GetValue() == branchId && p.IsActive));
}

#pragma warning restore S4144
