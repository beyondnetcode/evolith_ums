namespace Evolith.Ums.Domain.Identity.Repositories.TenantParameter;

using TenantParameterAggregate = Evolith.Ums.Domain.Identity.Tenant.TenantParameter.TenantParameter;

public interface ITenantParameterRepository : IAggregateRepository<TenantParameterAggregate>
{
    Task<TenantParameterAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TenantParameterAggregate?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantParameterAggregate>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantParameterAggregate>> GetActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantParameterAggregate>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);
}