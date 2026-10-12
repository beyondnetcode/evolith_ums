namespace Evolith.Ums.Domain.Kernel;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}

public interface IAggregateRepository<TAggregate>
    where TAggregate : class
{
    IUnitOfWork UnitOfWork { get; }
    Task<TAggregate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
