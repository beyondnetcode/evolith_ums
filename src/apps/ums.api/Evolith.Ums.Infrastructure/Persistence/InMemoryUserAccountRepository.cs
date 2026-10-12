#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

public sealed class InMemoryUserAccountRepository : IUserAccountRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, UserAccountAggregate> _store = new();
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public InMemoryUserAccountRepository(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // REC-05: Tenant filter for dev/test isolation
    private Guid? CurrentTenantId =>
        _httpContextAccessor?.HttpContext?.RequestServices
            .GetService<ITenantContext>()?.OrganizationId;

    public IUnitOfWork UnitOfWork => this;

    public Task<UserAccountAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var userAccount);
        userAccount?.BrokenRules.Clear();
        return Task.FromResult(userAccount);
    }

    public Task<UserAccountAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<UserAccountAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var tid = tenantId ?? CurrentTenantId;
        var all = (tid.HasValue
            ? _store.Values.Where(u => u.Props.TenantId.GetValue() == tid.Value)
            : _store.Values).ToList();
        all.ForEach(u => u.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<UserAccountAggregate>>(all);
    }

    public Task<UserAccountAggregate?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        var userAccount = _store.Values.FirstOrDefault(u => string.Equals(u.Props.Email.GetValue(), email.GetValue(), StringComparison.Ordinal));
        userAccount?.BrokenRules.Clear();
        return Task.FromResult<UserAccountAggregate?>(userAccount);
    }

    public Task<UserAccountAggregate?> GetByTenantAndEmailAsync(
        Guid tenantId,
        Email email,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var userAccount = _store.Values.FirstOrDefault(u =>
            u.Props.TenantId.GetValue() == tenantId &&
            string.Equals(u.Props.Email.GetValue(), email.GetValue(), StringComparison.Ordinal));

        userAccount?.BrokenRules.Clear();
        return Task.FromResult<UserAccountAggregate?>(userAccount);
    }

    public Task<IReadOnlyList<UserAccountAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var filtered = _store.Values.Where(u => u.Props.TenantId.GetValue() == tenantId).ToList();
        filtered.ForEach(u => u.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<UserAccountAggregate>>(filtered);
    }

    // REC-12: InMemory — in-memory pagination (acceptable for test/dev data volumes)
    public async Task<(IReadOnlyList<UserAccountAggregate> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, string? status, string sortBy, string sortOrder,
        Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(null, cancellationToken);
        var query = tenantId.HasValue
            ? all.Where(u => u.Props.TenantId.GetValue() == tenantId.Value)
            : all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(u => u.Props.Email.GetValue().ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.Props.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));

        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("email", "desc") => query.OrderByDescending(u => u.Props.Email.GetValue()),
            _                 => query.OrderBy(u => u.Props.Email.GetValue()),
        };

        var list = query.ToList();
        var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (paged, list.Count);
    }

    /// <inheritdoc/>
    /// REC-16: InMemory soft-delete removes the entry from the store entirely (no persistence).
    public Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken cancellationToken = default)
    {
        var removed = _store.TryRemove(id, out _);
        return Task.FromResult(removed);
    }

    public Task AddAsync(UserAccountAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserAccountAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(UserAccountAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public void Dispose() { }

    /// <summary>
    /// Prueba de «primer arranque». Aquí no hay filtros globales que desactivar: el almacén es el
    /// diccionario entero.
    /// </summary>
    public Task<bool> AnyUserAccountExistsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(!_store.IsEmpty);

    // ── Dependency guard queries ────────────────────────────────────────────

    public Task<int> CountActiveByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Count(u =>
            u.Props.TenantId.GetValue() == tenantId &&
            u.Props.Status == Evolith.Ums.Domain.Enums.UserStatus.Active));

    public Task<int> CountActiveByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Count(u =>
            u.Props.BranchId != null &&
            u.Props.BranchId.GetValue() == branchId &&
            u.Props.Status == Evolith.Ums.Domain.Enums.UserStatus.Active));
}

#pragma warning restore S4144
