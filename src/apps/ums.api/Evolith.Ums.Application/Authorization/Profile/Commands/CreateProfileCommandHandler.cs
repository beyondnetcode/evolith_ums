using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Commands;

using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Events;

public sealed class CreateProfileCommandHandler : ICommandHandler<CreateProfileCommand, CreateProfileResponse>
{
    private readonly IProfileRepository _profileRepository;
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly ITemplateAssignmentRuleRepository _assignmentRuleRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public CreateProfileCommandHandler(
        IProfileRepository profileRepository,
        IPermissionTemplateRepository templateRepository,
        ITemplateAssignmentRuleRepository assignmentRuleRepository,
        IUserAccountRepository userAccountRepository,
        IRoleRepository roleRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _profileRepository = profileRepository;
        _templateRepository = templateRepository;
        _assignmentRuleRepository = assignmentRuleRepository;
        _userAccountRepository = userAccountRepository;
        _roleRepository = roleRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_profiles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateProfileResponse>> Handle(
        CreateProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreateProfileResponse>.Failure("Authenticated user is required to create a profile.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<CreateProfileResponse>.Failure(scopeResult.Error);
        }

        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (userAccount is null)
        {
            return Result<CreateProfileResponse>.Failure("User account not found.");
        }

        if (userAccount.TenantId.GetValue() != request.TenantId)
        {
            return Result<CreateProfileResponse>.Failure(
                "User account does not belong to the requested tenant.");
        }

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<CreateProfileResponse>.Failure("Role not found.");
        }

        if (role.TenantId.GetValue() != request.TenantId)
        {
            return Result<CreateProfileResponse>.Failure(
                "Role does not belong to the requested tenant.");
        }

        // Un perfil por (usuario, rol, sucursal) activo. Sin esta guarda, reejecutar un
        // aprovisionamiento —o pulsar dos veces— dejaba al usuario con perfiles duplicados: la
        // plantilla se asigna a UNO, así que el otro queda con cero concesiones y el selector los
        // ofrece indistinguibles. Entrar por el vacío es entrar sin permisos, y todo respondió 201.
        //
        // Se comprueba sobre los perfiles ACTIVOS: uno desactivado es historia, y volver a dar de
        // alta el mismo rol tras retirarlo es legítimo.
        var perfilesDelUsuario = await _profileRepository.GetActiveByUserAndTenantAsync(
            request.UserId, request.TenantId, cancellationToken);

        // `is { Count: > 0 }` y no `.Any()` a secas: el contrato declara la lista no nula, pero un
        // doble de prueba que no configure esta consulta devuelve null, y una guarda que revienta
        // cuando su colaborador calla es peor que la ausencia de guarda.
        if (perfilesDelUsuario is { Count: > 0 } && perfilesDelUsuario.Any(p =>
                p.RoleId.GetValue() == request.RoleId &&
                p.BranchId?.GetValue() == request.BranchId))
        {
            return Result<CreateProfileResponse>.Failure(
                DomainErrors.Authorization.ProfileAlreadyExistsForRole);
        }

        var profileResult = Profile.Create(
            TenantId.Load(request.TenantId),
            UserId.Load(request.UserId),
            RoleId.Load(request.RoleId),
            request.BranchId.HasValue ? BranchId.Load(request.BranchId.Value) : null,
            ActorId.Create(_userContext.UserId));

        if (profileResult.IsFailure)
        {
            return Result<CreateProfileResponse>.Failure(profileResult.Error);
        }

        var profile = profileResult.Value;

        // G-043 (materialización de permisos): la plantilla auto-asignada por regla se materializa
        // ANTES de persistir, en la MISMA unidad de trabajo del agregado Profile (un agregado por tx).
        // Antes el perfil se guardaba primero y la asignación se intentaba en un segundo Save cuyo
        // fallo se tragaba (permissionCount=0 sin señal). Ahora los permisos se incorporan al agregado
        // y se persisten en un único Save; si la materialización falla, se aborta y se desenmascara el
        // error (Result.Failure con código) en vez de crear un perfil sin permisos silenciosamente.
        var materializeResult = await MaterializeAutoAssignedTemplateAsync(
            profile, request.TenantId, request.RoleId, cancellationToken);
        if (materializeResult.IsFailure)
        {
            return Result<CreateProfileResponse>.Failure(materializeResult.Error);
        }

        await _profileRepository.AddAsync(profile, cancellationToken);
        await _profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateProfileResponse>.Success(
            new CreateProfileResponse(profile.Props.Id.GetValue()));
    }

    private async Task<Result> MaterializeAutoAssignedTemplateAsync(
        Profile profile,
        Guid tenantId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var matchingRules = await _assignmentRuleRepository.GetActiveByTenantAndRoleAsync(
            tenantId, roleId, cancellationToken);

        var topRule = matchingRules?.FirstOrDefault();
        if (topRule is null)
        {
            // No hay regla de auto-asignación activa para (tenant, role): el perfil se crea sin
            // plantilla. Es un resultado LEGÍTIMO —no todo rol tiene regla— y no debe fallar.
            return Result.Success();
        }

        var template = await _templateRepository.GetByIdAsync(topRule.TemplateId.GetValue(), cancellationToken);
        if (template is null)
        {
            // La regla referencia una plantilla inexistente: es una inconsistencia de configuración
            // que DEBE aflorar (antes: return silencioso → permissionCount=0 sin diagnóstico).
            return Result.Failure(
                $"PROFILE_TEMPLATE_MISSING: la regla de asignación {topRule.Props.Id.GetValue()} referencia una plantilla inexistente ({topRule.TemplateId.GetValue()}).");
        }

        var assignResult = profile.AssignTemplate(template, ActorId.Create(_userContext.UserId));
        if (assignResult.IsFailure)
        {
            // Desenmascarar: antes el fallo de AssignTemplate se tragaba y el perfil quedaba sin
            // permisos. Ahora se propaga el error de dominio con un código estable.
            return Result.Failure($"PROFILE_TEMPLATE_ASSIGN_FAILED: {assignResult.Error}");
        }

        profile.DomainEvents.RaiseEvent(new TemplateAutoAssignedEvent(
            profile.Props.Id.GetValue(),
            template.Props.Id.GetValue(),
            topRule.Props.Id.GetValue()));

        return Result.Success();
    }
}
