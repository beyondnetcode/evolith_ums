namespace Evolith.Ums.Application.Authorization.Profile.Commands;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;

public sealed class AssignProfileTemplateCommandHandler : ICommandHandler<AssignProfileTemplateCommand>
{
    private readonly IProfileRepository _profileRepository;
    private readonly IPermissionTemplateRepository _permissionTemplateRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserManagementDelegationAccessService _delegationAccessService;

    public AssignProfileTemplateCommandHandler(
        IProfileRepository profileRepository,
        IPermissionTemplateRepository permissionTemplateRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy,
        IUserManagementDelegationAccessService delegationAccessService)
    {
        _profileRepository = profileRepository;
        _permissionTemplateRepository = permissionTemplateRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
        _delegationAccessService = delegationAccessService;
    }

    [AuthorizationAspect(ResourceCode = "manage_profiles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(AssignProfileTemplateCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to assign a template.");
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

        var template = await _permissionTemplateRepository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure("Permission template was not found.");
        }

        var actor = ActorId.Create(_userContext.UserId);

        // ADR-UMS-106 D2/D3: una version nueva SUPERSEDE a las de su misma linea —misma terna
        // (inquilino, rol, suite)—, y el superseder vive aqui porque `ProfilePermission` guarda solo
        // `TemplateId`: dentro del agregado no hay con que distinguir «otra plantilla» de «otra
        // version de la mia». Sin esto, asignar la version siguiente SUMABA sus permisos a los de la
        // anterior —medido en UAT: perfiles de 94 a 188, de 407 a 528— y el modelo de permisos
        // dejaba de ser el declarado. Las plantillas de OTRAS lineas se conservan: ese es el caso
        // que el modelo admite a proposito, con resolucion deny-wins entre ellas ([[G-272]]).
        var linea = await _permissionTemplateRepository.GetByTenantRoleSuiteAsync(
            template.TenantId.GetValue(),
            template.RoleId.GetValue(),
            template.SystemSuiteId.GetValue(),
            cancellationToken);

        var anterioresDeLaLinea = profile.Permissions
            .Where(p => p.IsActive)
            .Select(p => p.TemplateId.GetValue())
            .Distinct()
            .Where(id => id != request.TemplateId && linea.Any(t => t.GetId().GetValue() == id))
            .ToList();

        foreach (var anterior in anterioresDeLaLinea)
        {
            var superseder = profile.UnassignTemplate(TemplateId.Load(anterior), actor);
            if (superseder.IsFailure)
            {
                return superseder;
            }
        }

        var result = profile.AssignTemplate(template, actor);
        if (result.IsFailure)
        {
            return result;
        }

        await _profileRepository.UpdateAsync(profile, cancellationToken);
        await _profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
