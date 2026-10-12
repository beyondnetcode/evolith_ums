using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class BlockUserAccountCommandHandler : ICommandHandler<BlockUserAccountCommand>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IProfileRepository _profileRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserManagementDelegationAccessService _delegationAccessService;

    public BlockUserAccountCommandHandler(
        IUserAccountRepository userAccountRepository,
        IProfileRepository profileRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy,
        IUserManagementDelegationAccessService delegationAccessService)
    {
        _userAccountRepository = userAccountRepository;
        _profileRepository = profileRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
        _delegationAccessService = delegationAccessService;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(BlockUserAccountCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to block a user account.");
        }

        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);
        if (userAccount is null)
        {
            return Result.Failure("User account was not found.");
        }

        var ownerScopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(userAccount.TenantId.GetValue(), cancellationToken);
        if (ownerScopeResult.IsFailure)
        {
            var delegatedAccess = await _delegationAccessService.EnsureCanExecuteAsync(
                userAccount.TenantId.GetValue(),
                Evolith.Ums.Domain.Identity.UserManagementDelegation.DelegatedAction.BlockUser,
                null,
                cancellationToken);

            if (delegatedAccess.IsFailure)
            {
                return Result.Failure(ownerScopeResult.Error);
            }
        }

        // ── Dependency guard: active profiles ─────────────────────────────────
        // Blocking a user with active profiles would leave those profiles
        // pointing to a blocked user, making auth graph generation fail.
        var activeProfileCount = await _profileRepository.CountActiveByUserAsync(
            request.UserAccountId, cancellationToken);

        if (activeProfileCount > 0)
        {
            var deps = new List<BlockingDependency>
            {
                new("Profile", "Active", activeProfileCount),
            };
            return Result.Failure(BlockedOperationError.Encode(DomainErrors.UserAccount.HasActiveProfiles, deps));
        }

        var result = userAccount.Block(Reason.Create(request.Reason), ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _userAccountRepository.UpdateAsync(userAccount, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
