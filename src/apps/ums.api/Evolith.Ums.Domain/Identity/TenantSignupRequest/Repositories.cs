namespace Evolith.Ums.Domain.Identity;

using Evolith.Ums.Domain.Identity.TenantSignupRequest;
using TenantSignupRequestAggregate = Evolith.Ums.Domain.Identity.TenantSignupRequest.TenantSignupRequest;

public interface ITenantSignupRequestRepository
{
    Evolith.Ums.Domain.Kernel.IUnitOfWork UnitOfWork { get; }
    Task<TenantSignupRequestAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantSignupRequestAggregate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantSignupRequestAggregate>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TenantSignupRequestAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantSignupRequestAggregate aggregate, CancellationToken cancellationToken = default);
}
