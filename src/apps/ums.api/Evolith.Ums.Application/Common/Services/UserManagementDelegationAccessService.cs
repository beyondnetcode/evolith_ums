using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;

namespace Evolith.Ums.Application.Common.Interfaces;

public sealed class UserManagementDelegationAccessService : IUserManagementDelegationAccessService
{
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserManagementDelegationRepository _delegationRepository;
    private readonly IUserContext _userContext;

    public UserManagementDelegationAccessService(
        ITenantScopePolicy tenantScopePolicy,
        IUserAccountRepository userAccountRepository,
        IUserManagementDelegationRepository delegationRepository,
        IUserContext userContext)
    {
        _tenantScopePolicy = tenantScopePolicy;
        _userAccountRepository = userAccountRepository;
        _delegationRepository = delegationRepository;
        _userContext = userContext;
    }

    public async Task<Result> EnsureCanExecuteAsync(
        Guid targetTenantId,
        DelegatedAction action,
        Guid? targetScopeId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId) ||
            !Guid.TryParse(_userContext.UserId, out var currentUserId))
        {
            return Result.Failure("Authenticated user is required.");
        }

        var currentTenantId = ResolveCurrentTenantId();
        if (currentTenantId is null)
        {
            return Result.Failure("AUTH_013: Tenant context is required for management access.");
        }

        if (currentTenantId.Value != targetTenantId)
        {
            return Result.Failure(
                $"AUTH_014: Tenant mismatch. User belongs to tenant '{currentTenantId}', but request targets '{targetTenantId}'.");
        }

        var currentUser = await _userAccountRepository.GetByIdAsync(currentUserId, cancellationToken);
        if (currentUser is null)
        {
            return Result.Failure("Authenticated user account was not found.");
        }

        if (currentUser.Status != UserStatus.Active)
        {
            return Result.Failure("Authenticated user account is not active.");
        }

        var managementOwnerScope = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(targetTenantId, cancellationToken);
        if (managementOwnerScope.IsSuccess)
        {
            return Result.Success();
        }

        var delegations = await _delegationRepository.GetActiveAsync(targetTenantId, cancellationToken);
        var canExecute = delegations.Any(delegation =>
            delegation.DelegatedAdminId.GetValue() == currentUserId &&
            delegation.CanExecuteAction(action, targetScopeId));

        return canExecute
            ? Result.Success()
            : Result.Failure("AUTH_016: Delegated access is required for this operation.");
    }

    private Guid? ResolveCurrentTenantId()
    {
        if (Guid.TryParse(_userContext.TenantId, out var tenantId))
        {
            return tenantId;
        }

        return null;
    }
}
