#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Evolith.Ums.Domain.Kernel;

using UserManagementDelegationAggregate = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation;

public sealed class InMemoryUserManagementDelegationRepository : IUserManagementDelegationRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, UserManagementDelegationAggregate> _store = new();
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public InMemoryUserManagementDelegationRepository(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // REC-05: Tenant filter for dev/test isolation
    private Guid? CurrentTenantId =>
        _httpContextAccessor?.HttpContext?.RequestServices
            .GetService<ITenantContext>()?.OrganizationId;

    public IUnitOfWork UnitOfWork => this;

    public Task<UserManagementDelegationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var delegation);
        delegation?.BrokenRules.Clear();
        return Task.FromResult(delegation);
    }

    public Task<UserManagementDelegationAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<UserManagementDelegationAggregate>> GetByDelegatedAdminAsync(Guid delegatedAdminId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = _store.Values
            .Where(d => d.Props.DelegatedAdminId.GetValue() == delegatedAdminId && d.Props.TenantId.GetValue() == tenantId)
            .ToList();
        result.ForEach(d => d.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<UserManagementDelegationAggregate>>(result);
    }

    public Task<IReadOnlyList<UserManagementDelegationAggregate>> GetByDelegatingAdminAsync(Guid delegatingAdminId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = _store.Values
            .Where(d => d.Props.DelegatingAdminId.GetValue() == delegatingAdminId && d.Props.TenantId.GetValue() == tenantId)
            .ToList();
        result.ForEach(d => d.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<UserManagementDelegationAggregate>>(result);
    }

    public Task<IReadOnlyList<UserManagementDelegationAggregate>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = _store.Values
            .Where(d => d.Props.TenantId.GetValue() == tenantId && d.Props.Status == DelegationStatus.Active)
            .ToList();
        result.ForEach(d => d.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<UserManagementDelegationAggregate>>(result);
    }

    public Task<IReadOnlyList<UserManagementDelegationAggregate>> GetExpiredActiveAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        var result = _store.Values
            .Where(d => d.Props.Status == DelegationStatus.Active && d.Props.ValidUntil <= asOf)
            .ToList();
        result.ForEach(d => d.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<UserManagementDelegationAggregate>>(result);
    }

    public Task<IReadOnlyList<UserManagementDelegationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var tid = tenantId ?? CurrentTenantId;
        var all = (tid.HasValue
            ? _store.Values.Where(d => d.Props.TenantId.GetValue() == tid.Value)
            : _store.Values).ToList();
        all.ForEach(d => d.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<UserManagementDelegationAggregate>>(all);
    }

    public Task AddAsync(UserManagementDelegationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public void Seed(UserManagementDelegationAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public Task UpdateAsync(UserManagementDelegationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Dispose() { }
}

#pragma warning restore S4144
