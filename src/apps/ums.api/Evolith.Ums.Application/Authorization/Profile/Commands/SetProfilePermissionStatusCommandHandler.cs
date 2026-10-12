namespace Evolith.Ums.Application.Authorization.Profile.Commands;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;

public sealed class SetProfilePermissionStatusCommandHandler : ICommandHandler<SetProfilePermissionStatusCommand>
{
    private readonly IProfileRepository _profileRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserManagementDelegationAccessService _delegationAccessService;

    public SetProfilePermissionStatusCommandHandler(
        IProfileRepository profileRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy,
        IUserManagementDelegationAccessService delegationAccessService)
    {
        _profileRepository = profileRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
        _delegationAccessService = delegationAccessService;
    }

    [AuthorizationAspect(ResourceCode = "manage_profiles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SetProfilePermissionStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to change permission status.");
        }

        var profile = await _profileRepository.GetByIdAsync(request.ProfileId, cancellationToken);
        if (profile is null)
        {
            return Result.Failure("Profile was not found.");
        }

        var ownerScopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(profile.TenantId.GetValue(), cancellationToken);
        if (ownerScopeResult.IsFailure)
        {
            var delegatedAccess = await _delegationAccessService.EnsureCanExecuteAsync(
                profile.TenantId.GetValue(),
                Evolith.Ums.Domain.Identity.UserManagementDelegation.DelegatedAction.ManageProfilePermissions,
                profile.BranchId?.GetValue(),
                cancellationToken);

            if (delegatedAccess.IsFailure)
            {
                return Result.Failure(ownerScopeResult.Error);
            }
        }

        var actor = ActorId.Create(_userContext.UserId);
        var permissionId = IdValueObject.Load(request.PermissionId);
        var result = request.IsActive
            ? profile.ActivatePermission(permissionId, actor)
            : profile.DeactivatePermission(permissionId, actor);

        if (result.IsFailure)
        {
            return result;
        }

        await _profileRepository.UpdateAsync(profile, cancellationToken);
        await _profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
