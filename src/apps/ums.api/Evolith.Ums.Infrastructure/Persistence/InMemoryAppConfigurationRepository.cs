#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Kernel;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

public sealed class InMemoryAppConfigurationRepository : IAppConfigurationRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, AppConfigurationAggregate> _store = new();
    public IUnitOfWork UnitOfWork => this;

    // Las lecturas ocultan lo eliminado lógicamente; el agregado NUNCA sale del diccionario.
    public Task<AppConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var entity);
        entity?.BrokenRules.Clear();
        return Task.FromResult(entity is not null && entity.Status != ConfigStatus.Deleted ? entity : null);
    }

    public Task<AppConfigurationAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    // Lookup de la ranura (ámbito, código): devuelve quién la ocupa AHORA, nunca una lápida. Espejo
    // exacto del índice único parcial de PostgreSQL; si esta copia viera lo eliminado, las pruebas
    // en memoria certificarían un comportamiento que la base ya no tiene. El orden desempata igual:
    // gana la no archivada y, entre iguales, la más reciente.
    public Task<AppConfigurationAggregate?> GetByScopeAndCodeAsync(Guid? tenantId, Guid? systemSuiteId, Guid? moduleId, string code, CancellationToken cancellationToken = default)
    {
        var entity = _store.Values
            .Where(item =>
                string.Equals(item.Props.Code.GetValue(), code, StringComparison.OrdinalIgnoreCase)
                && item.Props.TenantId?.GetValue() == tenantId
                && item.Props.SystemSuiteId?.GetValue() == systemSuiteId
                && item.Props.ModuleId?.GetValue() == moduleId
                && item.Status != ConfigStatus.Deleted)
            .OrderBy(item => item.Status == ConfigStatus.Archived ? 1 : 0)
            .ThenByDescending(item => item.Props.Audit.GetValue().CreatedAt)
            .FirstOrDefault();
        entity?.BrokenRules.Clear();
        return Task.FromResult(entity);
    }

    public Task<IReadOnlyList<AppConfigurationAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var live = _store.Values.Where(item => item.Status != ConfigStatus.Deleted);
        var items = tenantId.HasValue
            ? live.Where(item => item.Props.TenantId?.GetValue() == tenantId.Value).ToList()
            : live.ToList();
        items.ForEach(item => item.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<AppConfigurationAggregate>>(items);
    }

    public Task AddAsync(AppConfigurationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AppConfigurationAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    // REC-10: InMemory has no RowVersion — concurrency token is ignored
    public Task UpdateAsync(AppConfigurationAggregate aggregate, byte[]? expectedRowVersion, CancellationToken cancellationToken = default)
        => UpdateAsync(aggregate, cancellationToken);

    // Sin DeleteAsync: el borrado lógico se persiste por UpdateAsync (el agregado ya trae Deleted).

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <summary>
    /// Lectura SIN filtros de ocultación: espejo en memoria de `IgnoreQueryFilters()`.
    ///
    /// Existe porque ADR-0164 §5 exige demostrar que la fila sobrevive al borrado, y preguntar a la
    /// API no distingue «oculto» de «borrado». Desde que la ranura se libera, `GetByScopeAndCodeAsync`
    /// devuelve solo la viva, así que ya no sirve como sonda del almacén. Ninguna ruta de producción
    /// la usa: es la contraparte del `IgnoreQueryFilters()` explícito del repositorio PostgreSQL.
    /// </summary>
    public IReadOnlyList<AppConfigurationAggregate> GetAllIncludingDeleted() => _store.Values.ToList();

    public void Seed(AppConfigurationAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }

    public void Dispose() { }
}

#pragma warning restore S4144
