using Evolith.Ums.Domain.Identity.Repositories.TenantParameter;
using Evolith.Ums.Domain.Identity.Tenant.TenantParameter;
using Evolith.Ums.Domain.Kernel;

namespace Evolith.Ums.Infrastructure.Persistence.Identity.TenantParameter;

using TenantParameterAggregate = Evolith.Ums.Domain.Identity.Tenant.TenantParameter.TenantParameter;

public sealed class InMemoryTenantParameterRepository : ITenantParameterRepository, IUnitOfWork
{
    private readonly List<TenantParameterAggregate> _parameters = [];
    private readonly HashSet<Guid> _committedIds = [];

    public IUnitOfWork UnitOfWork => this;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var parameter in _parameters.Where(p => !_committedIds.Contains(p.GetId().GetValue())))
        {
            _committedIds.Add(parameter.GetId().GetValue());
        }
        return await Task.FromResult(true);
    }

    public void Dispose()
    {
        // No-op for in-memory
    }

    public Task AddAsync(TenantParameterAggregate aggregate, CancellationToken cancellationToken = default)
    {
        _parameters.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TenantParameterAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var index = _parameters.FindIndex(p => p.GetId().GetValue() == aggregate.GetId().GetValue());
        if (index >= 0) _parameters[index] = aggregate;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Borrado LÓGICO: la entrada NO se saca de la lista; antes hacía <c>RemoveAll</c> y perdía la
    /// fila. El agregado ya trae la marca puesta por <c>TenantParameter.Delete</c> (aquí la lista
    /// guarda la MISMA instancia) y las lecturas de abajo lo ocultan. Igual que el store PostgreSQL,
    /// se corta si nadie pasó por el dominio.
    /// </summary>
    public Task DeleteAsync(TenantParameterAggregate aggregate, CancellationToken cancellationToken = default)
    {
        if (!aggregate.IsDeleted)
        {
            throw new InvalidOperationException(
                $"Tenant parameter {aggregate.GetId().GetValue()} must be logically deleted via TenantParameter.Delete before persisting.");
        }

        var index = _parameters.FindIndex(p => p.GetId().GetValue() == aggregate.GetId().GetValue());
        if (index >= 0) _parameters[index] = aggregate;
        return Task.CompletedTask;
    }

    public async Task<TenantParameterAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_parameters.FirstOrDefault(p => !p.IsDeleted && p.GetId().GetValue() == id));
    }

    public async Task<TenantParameterAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_parameters.FirstOrDefault(p =>
            !p.IsDeleted && p.TenantId.GetValue() == tenantId && p.GetId().GetValue() == id));
    }

    Task<TenantParameterAggregate?> IAggregateRepository<TenantParameterAggregate>.GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        return GetByIdAsync(tenantId, id, cancellationToken);
    }

    public async Task<TenantParameterAggregate?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_parameters.FirstOrDefault(p =>
            !p.IsDeleted && p.TenantId.GetValue() == tenantId && p.Code.GetValue() == code));
    }

    public async Task<IReadOnlyList<TenantParameterAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_parameters.Where(p => !p.IsDeleted && p.TenantId.GetValue() == tenantId).ToList());
    }

    public async Task<IReadOnlyList<TenantParameterAggregate>> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_parameters.Where(p => !p.IsDeleted && p.TenantId.GetValue() == tenantId && p.IsActive).ToList());
    }

    public async Task<IReadOnlyList<TenantParameterAggregate>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_parameters.Where(p =>
            !p.IsDeleted && p.TenantId.GetValue() == tenantId && p.IsActive && p.Category.Name.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(_parameters.Any(p =>
            !p.IsDeleted && p.TenantId.GetValue() == tenantId && p.Code.GetValue() == code && p.IsActive));
    }

    public void Seed(TenantParameterAggregate aggregate)
    {
        _parameters.Add(aggregate);
        _committedIds.Add(aggregate.GetId().GetValue());
    }
}