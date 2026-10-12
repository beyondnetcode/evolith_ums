using Evolith.Ums.Domain.Identity.UserManagementDelegation;

namespace Evolith.Ums.Application.Common.Interfaces;

public interface IUserManagementDelegationAccessService
{
    Task<Result> EnsureCanExecuteAsync(
        Guid targetTenantId,
        DelegatedAction action,
        Guid? targetScopeId = null,
        CancellationToken cancellationToken = default);
}
