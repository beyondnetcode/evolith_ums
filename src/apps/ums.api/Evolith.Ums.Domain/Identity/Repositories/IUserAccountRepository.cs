namespace Evolith.Ums.Domain.Identity.Repositories;

using UserAccountEntity = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

public interface IUserAccountRepository
{
    Task<UserAccountEntity?> FindByIdAsync(UserAccountId userId, TenantId rootTenantId);
    Task<UserAccountEntity?> FindByEmailAsync(Email email, TenantId tenantId);
    Task<IReadOnlyCollection<UserAccountEntity>> FindByTenantAsync(TenantId tenantId, UserStatus? status = null);
    Task<IReadOnlyCollection<UserAccountEntity>> FindPendingApprovalAsync(TenantId tenantId);
    Task<UserAccountEntity?> FindByIdentityReferenceAsync(IdentityReference reference, IdentityReferenceType referenceType, TenantId tenantId);
    Task AddAsync(UserAccountEntity userAccount);
    Task UpdateAsync(UserAccountEntity userAccount);
}
