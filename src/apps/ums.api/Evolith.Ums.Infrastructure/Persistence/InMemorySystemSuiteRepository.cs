#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;

public sealed class InMemorySystemSuiteRepository : ISystemSuiteRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, SystemSuiteAggregate> _store = new();

    public IUnitOfWork UnitOfWork => this;

    /// <summary>
    /// Equivalente en memoria del filtro global de PostgreSQL (G-246): el sistema eliminado
    /// lógicamente sigue en el almacén —igual que la fila sigue en la tabla— pero no se devuelve en
    /// ninguna lectura. Sin esto, el host en memoria y el de PostgreSQL responderían distinto a la
    /// misma petición, que es la clase de divergencia que hace inútil un doble.
    /// </summary>
    private static bool EsVisible(SystemSuiteAggregate suite)
        => suite.Props.Status.Id != Evolith.Ums.Domain.Enums.SystemStatus.Deleted.Id;

    public Task<SystemSuiteAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var systemSuite);
        if (systemSuite is not null && !EsVisible(systemSuite))
        {
            systemSuite = null;
        }

        systemSuite?.BrokenRules.Clear();
        return Task.FromResult(systemSuite);
    }

    public Task<SystemSuiteAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<SystemSuiteAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var visibles = _store.Values.Where(EsVisible);
        var all = tenantId.HasValue
            ? visibles.Where(s => s.Props.TenantId.GetValue() == tenantId.Value).ToList()
            : visibles.ToList();
        all.ForEach(s => s.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<SystemSuiteAggregate>>(all);
    }

    // Sonda de unicidad del código, no lectura del catálogo: incluye a propósito los eliminados
    // lógicamente, igual que su equivalente de PostgreSQL, porque la lápida conserva el código.
    public Task<SystemSuiteAggregate?> GetByCodeAsync(Code code, CancellationToken cancellationToken = default)
    {
        var systemSuite = _store.Values
            .FirstOrDefault(s => string.Equals(s.Props.Code.GetValue(), code.GetValue(), StringComparison.Ordinal));
        systemSuite?.BrokenRules.Clear();
        return Task.FromResult<SystemSuiteAggregate?>(systemSuite);
    }

    public Task<IReadOnlyList<SystemSuiteAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var filtered = _store.Values.Where(EsVisible).Where(s => s.Props.TenantId.GetValue() == tenantId).ToList();
        filtered.ForEach(s => s.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<SystemSuiteAggregate>>(filtered);
    }

    public Task<IReadOnlyList<Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteSummary>> GetSummariesByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var items = _store.Values
            .Where(EsVisible)
            .Where(x => ids.Contains(x.GetId().GetValue()))
            .Select(x => new Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteSummary(x.GetId().GetValue(), x.Props.Code.GetValue(), x.Props.Name.GetValue()))
            .ToList();

        return Task.FromResult<IReadOnlyList<Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteSummary>>(items);
    }

    public Task<Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuitePage> GetPageAsync(
        Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuitePageQuery query,
        CancellationToken cancellationToken = default)
    {
        var items = _store.Values.Where(EsVisible);

        if (query.TenantId.HasValue)
            items = items.Where(x => x.Props.TenantId.GetValue() == query.TenantId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
            items = items.Where(x => string.Equals(x.Props.Status.ToString(), query.Status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search;
            items = query.SearchField switch
            {
                "code" => items.Where(x => x.Props.Code.GetValue().Contains(s, StringComparison.OrdinalIgnoreCase)),
                "id"   => items.Where(x => x.GetId().GetValue().ToString().Contains(s, StringComparison.OrdinalIgnoreCase)),
                _      => items.Where(x => x.Props.Name.GetValue().Contains(s, StringComparison.OrdinalIgnoreCase)),
            };
        }

        var lista = items.ToList();

        var ordenados = (query.SortBy, query.Descending) switch
        {
            ("code", true)    => lista.OrderByDescending(x => x.Props.Code.GetValue()),
            ("code", false)   => lista.OrderBy(x => x.Props.Code.GetValue()),
            ("status", true)  => lista.OrderByDescending(x => x.Props.Status.ToString()),
            ("status", false) => lista.OrderBy(x => x.Props.Status.ToString()),
            (_, true)         => lista.OrderByDescending(x => x.Props.Name.GetValue()),
            _                 => lista.OrderBy(x => x.Props.Name.GetValue()),
        };

        var ids = ordenados
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => x.GetId().GetValue())
            .ToList();

        return Task.FromResult(new Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuitePage(ids, lista.Count));
    }

    public Task<IReadOnlyList<SystemSuiteAggregate>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var porId = _store.Values.Where(EsVisible).ToDictionary(x => x.GetId().GetValue());
        var items = ids.Where(porId.ContainsKey).Select(id => porId[id]).ToList();
        items.ForEach(item => item.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<SystemSuiteAggregate>>(items);
    }

    public Task AddAsync(SystemSuiteAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SystemSuiteAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    // El almacén en memoria solo conoce sistemas: no ve roles, plantillas ni inquilinos, así que no
    // puede responder por ellos. Declara «sin referencias vivas» en lugar de inventar un bloqueo; la
    // guarda de cascada se ejerce y se verifica contra PostgreSQL (G-246).
    public Task<Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteDependents> GetDependentsAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuiteDependents.None);

    // Aquí tampoco hay borrado: `UpdateAsync` reemplaza el agregado con su estado `Deleted` y a
    // partir de ahí `EsVisible` lo oculta. El sistema sigue en el almacén, igual que la fila sigue
    // en la tabla.

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(SystemSuiteAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public void Dispose() { }
}

#pragma warning restore S4144
