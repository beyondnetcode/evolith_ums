#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
#pragma warning disable S1144
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant.Branch;
using Evolith.Ums.Domain.Kernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;

public sealed class InMemoryTenantRepository : ITenantRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, TenantAggregate> _store = new();
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public InMemoryTenantRepository(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    // REC-05: Returns OrganizationId from the current HTTP request scope, or null (no filter) if none.
    private Guid? CurrentTenantId =>
        _httpContextAccessor?.HttpContext?.RequestServices
            .GetService<ITenantContext>()?.OrganizationId;

    public IUnitOfWork UnitOfWork => this;

    public Task<TenantAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var tenant);
        tenant?.BrokenRules.Clear();
        return Task.FromResult(tenant);
    }

    public Task<TenantAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    // G-161: en memoria no hay filtro global, así que la colección está siempre completa.
    // ADR-0164 §2.3: incluye las cerradas — su código sigue ocupado.
    public Task<bool> BranchCodeExistsAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(tenantId, out var tenant);
        var exists = tenant is not null
            && tenant.Branches.Any(b => b.Code.GetValue() == code);
        return Task.FromResult(exists);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// El almacén en memoria conserva la MISMA instancia del agregado, así que los asientos que el
    /// dominio anotó siguen en el búfer de la sucursal: no hay volcado que simular. Basta con leerlos
    /// en orden para que la bitácora sea consultable igual que contra PostgreSQL.
    /// </remarks>
    public Task<IReadOnlyList<BranchLifecycleEntry>> GetBranchLifecycleAsync(
        Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(tenantId, out var tenant);

        var asientos = tenant?.Branches
            .FirstOrDefault(b => b.Props.Id.GetValue() == branchId)?
            .PendingLifecycleEntries
            .OrderBy(e => e.OccurredAtUtc)
            .ThenBy(e => e.Episode.Id)
            .ToList() ?? [];

        return Task.FromResult<IReadOnlyList<BranchLifecycleEntry>>(asientos);
    }

    public Task<IReadOnlyList<TenantAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var all = _store.Values.ToList();

        if (tenantId.HasValue)
        {
            // Scope to the tenant itself and any direct child tenants
            all = all
                .Where(t => t.Props.Id.GetValue() == tenantId.Value
                         || t.Props.ParentTenantId?.GetValue() == tenantId.Value)
                .ToList();
        }

        all.ForEach(t => t.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<TenantAggregate>>(all);
    }

    // REC-12: InMemory — delegate to in-memory filter (acceptable for test/dev data volumes)
    public async Task<(IReadOnlyList<TenantAggregate> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, string? status, string sortBy, string sortOrder,
        Guid? tenantId = null, CancellationToken cancellationToken = default, string? searchField = null)
    {
        var all = await GetAllAsync(tenantId, cancellationToken);
        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            // El campo de búsqueda lo determina `searchField` (parámetro `criteria` del API), no el orden.
            var field = string.IsNullOrWhiteSpace(searchField) ? sortBy : searchField;
            query = field.ToLower() == "code"
                ? query.Where(t => t.Code.GetValue().ToLower().Contains(lower))
                : query.Where(t => t.Props.Name.GetValue().ToLower().Contains(lower));
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, QueryDefaults.AllStatuses, StringComparison.OrdinalIgnoreCase))
            query = query.Where(t => t.Props.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));

        query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
        {
            ("code", QueryDefaults.Descending) => query.OrderByDescending(t => t.Code.GetValue()),
            ("code", _)      => query.OrderBy(t => t.Code.GetValue()),
            ("name", QueryDefaults.Descending) => query.OrderByDescending(t => t.Props.Name.GetValue()),
            _                => query.OrderBy(t => t.Props.Name.GetValue()),
        };

        var list = query.ToList();
        var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (paged, list.Count);
    }

    public Task<TenantAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tenant = _store.Values.FirstOrDefault(t => string.Equals(t.Code.GetValue(), code, StringComparison.Ordinal));
        tenant?.BrokenRules.Clear();
        return Task.FromResult<TenantAggregate?>(tenant);
    }

    /// <inheritdoc/>
    /// REC-16: InMemory soft-delete removes the entry from the store (no persistence layer).
    public Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken cancellationToken = default)
    {
        var removed = _store.TryRemove(id, out _);
        return Task.FromResult(removed);
    }

    public Task AddAsync(TenantAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TenantAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <summary>
    /// Dev-only seed helper. The aggregate must already carry the desired deterministic Id.
    /// </summary>
    public void Seed(TenantAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public void Dispose() { }
}

#pragma warning restore S1144

#pragma warning restore S4144
