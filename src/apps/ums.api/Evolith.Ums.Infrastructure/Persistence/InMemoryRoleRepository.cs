#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Domain.Authorization;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;

public sealed class InMemoryRoleRepository : IRoleRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, RoleAggregate> _store = new();
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public InMemoryRoleRepository(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid? CurrentTenantId =>
        _httpContextAccessor?.HttpContext?.RequestServices
            .GetService<ITenantContext>()?.OrganizationId;

    public IUnitOfWork UnitOfWork => this;

    public Task<RoleAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var role);
        if (CurrentTenantId.HasValue && role?.TenantId.GetValue() != CurrentTenantId.Value)
        {
            role = null;
        }
        role?.BrokenRules.Clear();
        return Task.FromResult(role);
    }

    public Task<RoleAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<RoleAggregate?> GetByCodeAsync(Guid systemSuiteId, Code code, CancellationToken cancellationToken = default)
    {
        var role = _store.Values.FirstOrDefault(x =>
            x.SystemSuiteId.GetValue() == systemSuiteId &&
            (!CurrentTenantId.HasValue || x.TenantId.GetValue() == CurrentTenantId.Value) &&
            x.Code.GetValue() == code.GetValue());
        role?.BrokenRules.Clear();
        return Task.FromResult(role);
    }

    public Task<IReadOnlyList<RoleAggregate>> GetBySystemSuiteIdAsync(Guid systemSuiteId, CancellationToken cancellationToken = default)
    {
        var roles = _store.Values
            .Where(x => x.SystemSuiteId.GetValue() == systemSuiteId)
            .Where(x => !CurrentTenantId.HasValue || x.TenantId.GetValue() == CurrentTenantId.Value)
            .OrderBy(x => x.HierarchyLevel)
            .ThenBy(x => x.PromotionOrder)
            .ToList();
        roles.ForEach(x => x.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<RoleAggregate>>(roles);
    }

    public Task<IReadOnlyList<RoleAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var roles = _store.Values.Where(x => x.TenantId.GetValue() == tenantId).ToList();
        roles.ForEach(x => x.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<RoleAggregate>>(roles);
    }

    public void Seed(RoleAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public Task<IReadOnlyList<RoleAggregate>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var items = _store.Values.Where(x => ids.Contains(x.GetId().GetValue())).ToList();
        items.ForEach(item => item.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<RoleAggregate>>(items);
    }

    public Task AddAsync(RoleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.GetId().GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RoleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.GetId().GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void Dispose() { }

    // ── Dependency guard queries ────────────────────────────────────────────

    public Task<int> CountActiveChildRolesAsync(Guid parentRoleId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Count(r => r.Props.ParentRoleId?.GetValue() == parentRoleId && r.IsActive));
}

#pragma warning restore S4144
