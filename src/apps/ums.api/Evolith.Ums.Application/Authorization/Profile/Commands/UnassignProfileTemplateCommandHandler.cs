namespace Evolith.Ums.Application.Authorization.Profile.Commands;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;

/// <summary>
/// Desliga una plantilla de un perfil (ADR-UMS-106 D4).
///
/// <para>Misma politica de ambito que asignar: es la operacion inversa y retira permisos a quien los
/// tenia, asi que no puede estar peor guardada que la que los concede.</para>
/// </summary>
public sealed class UnassignProfileTemplateCommandHandler : ICommandHandler<UnassignProfileTemplateCommand>
{
    private readonly IProfileRepository _profileRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserManagementDelegationAccessService _delegationAccessService;

    public UnassignProfileTemplateCommandHandler(
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
    public async Task<Result> Handle(UnassignProfileTemplateCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to unassign a template.");
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
                Evolith.Ums.Domain.Identity.UserManagementDelegation.DelegatedAction.AssignProfile,
                profile.BranchId?.GetValue(),
                cancellationToken);

            if (delegatedAccess.IsFailure)
            {
                return Result.Failure(ownerScopeResult.Error);
            }
        }

        var result = profile.UnassignTemplate(TemplateId.Load(request.TemplateId), ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _profileRepository.UpdateAsync(profile, cancellationToken);
        await _profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
