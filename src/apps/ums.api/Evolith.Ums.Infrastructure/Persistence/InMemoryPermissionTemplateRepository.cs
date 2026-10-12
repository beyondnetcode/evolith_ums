#pragma warning disable S4144 // Scaffolding intencional para testing In-Memory
namespace Evolith.Ums.Infrastructure.Persistence;

using System.Collections.Concurrent;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;
using PermissionTemplateAggregate = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate;

public sealed class InMemoryPermissionTemplateRepository : IPermissionTemplateRepository, IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, PermissionTemplateAggregate> _store = new();

    public IUnitOfWork UnitOfWork => this;

    public Task<PermissionTemplateAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Las lecturas ocultan lo lógicamente eliminado, igual que el store PostgreSQL: quien pida
        // por id una plantilla eliminada debe recibir 404, no el agregado en estado terminal.
        _store.TryGetValue(id, out var template);
        if (template is not null && template.IsDeleted) template = null;
        template?.BrokenRules.Clear();
        return Task.FromResult(template);
    }

    public Task<PermissionTemplateAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<PermissionTemplateAggregate>> GetAllAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var vivas = _store.Values.Where(t => !t.IsDeleted);
        var all = tenantId.HasValue
            ? vivas.Where(t => t.Props.TenantId.GetValue() == tenantId.Value).ToList()
            : vivas.ToList();
        all.ForEach(t => t.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<PermissionTemplateAggregate>>(all);
    }

    public Task<IReadOnlyList<PermissionTemplateAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var filtered = _store.Values.Where(t => !t.IsDeleted && t.Props.TenantId.GetValue() == tenantId).ToList();
        filtered.ForEach(t => t.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<PermissionTemplateAggregate>>(filtered);
    }

    // Sin filtro de eliminadas, igual que el store PostgreSQL: alimenta el cálculo de la versión
    // siguiente y una plantilla eliminada sigue ocupando su versión en el índice único.
    public Task<IReadOnlyList<PermissionTemplateAggregate>> GetByTenantRoleSuiteAsync(Guid tenantId, Guid roleId, Guid systemSuiteId, CancellationToken cancellationToken = default)
    {
        var filtered = _store.Values.Where(t =>
            t.Props.TenantId.GetValue() == tenantId &&
            t.Props.RoleId.GetValue() == roleId &&
            t.Props.SystemSuiteId.GetValue() == systemSuiteId).ToList();
        filtered.ForEach(t => t.BrokenRules.Clear());
        return Task.FromResult<IReadOnlyList<PermissionTemplateAggregate>>(filtered);
    }

    public Task AddAsync(PermissionTemplateAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(PermissionTemplateAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _store[aggregate.Props.Id.GetValue()] = aggregate;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Borrado LÓGICO: la entrada se queda en el store; antes hacía <c>TryRemove</c> y perdía la fila.
    /// Replica el contrato booleano del store real: <c>false</c> si no existe o si ya estaba eliminada.
    /// </summary>
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(id, out var template) || template.IsDeleted) return Task.FromResult(false);
        return Task.FromResult(true);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    public Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Seed(PermissionTemplateAggregate aggregate)
    {
        aggregate.DomainEvents.MarkChangesAsCommitted();
        _store[aggregate.Props.Id.GetValue()] = aggregate;
    }
    public void Dispose() { }

    // ── Dependency guard queries ────────────────────────────────────────────

    public Task<int> CountPublishedByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Count(t =>
            t.Props.RoleId.GetValue() == roleId &&
            t.Status == Evolith.Ums.Domain.Enums.TemplateStatus.Published));

    // Los ítems de una plantilla eliminada no bloquean: su contenedor ya está lógicamente eliminado.
    public Task<int> CountItemsByTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Where(t => !t.IsDeleted).SelectMany(t => t.Items)
            .Count(i => i.TargetId.GetValue() == targetId && i.IsActive));
}

#pragma warning restore S4144
