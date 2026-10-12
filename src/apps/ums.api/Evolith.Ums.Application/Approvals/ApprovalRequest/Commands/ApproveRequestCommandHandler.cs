using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Commands;

using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Domain.Events;

public sealed class ApproveRequestCommandHandler : ICommandHandler<ApproveRequestCommand>
{
    private readonly IApprovalRequestRepository _repository;
    private readonly IProfileRepository _profileRepository;
    private readonly IApprovalWorkflowRepository _workflowRepository;
    private readonly IUserDocumentRepository _userDocumentRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserManagementDelegationRepository _delegationRepository;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUnitOfWorkScope _unitOfWorkScope;
    private readonly INotificationService _notificationService;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserContext _userContext;

    public ApproveRequestCommandHandler(
        IApprovalRequestRepository repository,
        IProfileRepository profileRepository,
        IApprovalWorkflowRepository workflowRepository,
        IUserDocumentRepository userDocumentRepository,
        IUserAccountRepository userAccountRepository,
        ITenantRepository tenantRepository,
        IUserManagementDelegationRepository delegationRepository,
        ITenantScopePolicy tenantScopePolicy,
        IUnitOfWorkScope unitOfWorkScope,
        INotificationService notificationService,
        IRoleRepository roleRepository,
        IUserContext userContext)
    {
        _repository = repository;
        _profileRepository = profileRepository;
        _workflowRepository = workflowRepository;
        _userDocumentRepository = userDocumentRepository;
        _userAccountRepository = userAccountRepository;
        _tenantRepository = tenantRepository;
        _delegationRepository = delegationRepository;
        _tenantScopePolicy = tenantScopePolicy;
        _unitOfWorkScope = unitOfWorkScope;
        _notificationService = notificationService;
        _roleRepository = roleRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approvals", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ApproveRequestCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var entity = await _repository.GetByIdAsync(request.ApprovalRequestId, cancellationToken);
        if (entity is null) return Result.Failure("Approval request not found.");

        // G-119 (SoD): segregación de deberes en la aprobación EXPLÍCITA — quien creó la solicitud
        // (Audit.CreatedBy) no puede aprobarla. Se guarda aquí (acción humana vía endpoint), no en el
        // dominio, para NO bloquear el auto-approve de workflows con RequiresApproval=false (que aprueba
        // con el actor creador como acción del sistema, sin intervención humana). Consistente con IGA.
        if (string.Equals(_userContext.UserId, entity.Props.Audit.GetValue().CreatedBy, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(DomainErrors.Approvals.SelfApprovalNotAllowed);

        var targetUser = await _userAccountRepository.GetByIdAsync(entity.TargetUserId.GetValue(), cancellationToken);
        if (targetUser is null)
            return Result.Failure("Target user not found.");

        var authorization = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(targetUser.TenantId.GetValue(), cancellationToken);
        if (authorization.IsFailure && !await CanApproveAsDelegatedBranchManagerAsync(entity, targetUser.TenantId.GetValue(), cancellationToken))
            return Result.Failure(authorization.Error);

        // G-051 F4: exigencia cross-agregado del checklist de documentos requeridos.
        // Precondicion de SOLO LECTURA y fail-closed: se cruza el checklist declarado por el
        // ApprovalWorkflow contra los UserDocument del usuario objetivo ANTES de transicionar.
        // No muta ApprovalWorkflow ni UserDocument, por lo que no amplia la excepcion D-016 de
        // este handler (que sigue mutando solo ApprovalRequest + Profile). Ver RequiredDocumentChecklist.
        var checklistResult = await EnsureRequiredDocumentsComplete(entity, targetUser, cancellationToken);
        if (checklistResult.IsFailure)
            return checklistResult;

        var result = entity.Approve(ActorId.Create(_userContext.UserId), RoleId.Load(request.GrantedRoleId), request.DecisionReason);
        if (result.IsFailure) return result;

        var grantedRoleId = RoleId.Load(request.GrantedRoleId);
        var assignedProfileResult = await ResolveOrCreateProfileAsync(entity, targetUser, grantedRoleId, cancellationToken);
        if (assignedProfileResult.IsFailure)
            return Result.Failure(assignedProfileResult.Error);

        var (assignedProfile, isNewProfile) = assignedProfileResult.Value;
        entity.Props.TargetProfileId = assignedProfile.GetId();
        entity.DomainEvents.RaiseEvent(new ProfileAssignedToUserEvent(
            targetUser.TenantId.GetValue(),
            entity.Props.Id.GetValue(),
            targetUser.GetId().GetValue(),
            assignedProfile.GetId().GetValue(),
            grantedRoleId.GetValue(),
            entity.RequestedBranchId?.GetValue(),
            _userContext.UserId,
            DateTime.UtcNow));

        // TODO(D-016): excepción PROVISIONAL a "un agregado por transacción" (ADR-0098 D2):
        // muta ApprovalRequest + Profile en la misma tx. Candidata a separación por despacho
        // post-commit / consistencia eventual (ADR-0098 D4). Ver DECISIONS.md D-016 (E2) y GAPS.md G-066.
        // Patrón e interpretación: KB-TXN-001 (Base de Conocimiento de Arquitectura, evolith-core).
        // Revisar al separar agregados en la progresión a microservicios.
        // G-117: begin/commit vía ExecutionStrategy. El BeginAsync manual anterior era incompatible con
        // EnableRetryOnFailure (NpgsqlRetryingExecutionStrategy) → toda aprobación fallaba con
        // InvalidOperationException 'does not support user-initiated transactions' (mapeada a 400).
        await _unitOfWorkScope.ExecuteInTransactionAsync(async ct =>
        {
            if (isNewProfile)
                await _profileRepository.AddAsync(assignedProfile, ct);
            else
                await _profileRepository.UpdateAsync(assignedProfile, ct);
            await _repository.UpdateAsync(entity, ct);
            await _profileRepository.UnitOfWork.SaveEntitiesAsync(ct);
            await _repository.UnitOfWork.SaveEntitiesAsync(ct);
        }, cancellationToken);

        var tenant = await _tenantRepository.GetByIdAsync(targetUser.TenantId.GetValue(), cancellationToken);
        await _notificationService.SendAsync(
            NotificationTemplates.ProfileRequestApproved(
                targetUser.Email.GetValue(),
                targetUser.DisplayName?.GetValue() ?? targetUser.Email.GetValue(),
                tenant?.Name.GetValue() ?? "your organization",
                entity.RequestedSystemId.GetValue().ToString(),
                entity.GrantedRoleId!.GetValue().ToString()),
            cancellationToken);

        return Result.Success();
    }

    // G-051 F4: valida (fail-closed) que el usuario objetivo satisfaga el checklist de documentos
    // obligatorios del workflow de la solicitud antes de aprobar. Si el workflow no se puede
    // resolver, se rechaza la aprobacion: no se aprueba sin poder verificar el cumplimiento.
    private async Task<Result> EnsureRequiredDocumentsComplete(
        ApprovalRequest entity,
        UserAccount targetUser,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflowRepository.GetByIdAsync(entity.WorkflowId.GetValue(), cancellationToken);
        if (workflow is null)
            return Result.Failure(DomainErrors.Approvals.RequiredDocumentsIncomplete);

        var targetUserDocuments = await _userDocumentRepository.GetByUserIdAsync(
            targetUser.GetId().GetValue(), cancellationToken);

        return RequiredDocumentChecklist.Evaluate(workflow, targetUserDocuments);
    }

    private async Task<bool> CanApproveAsDelegatedBranchManagerAsync(
        ApprovalRequest entity,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_userContext.UserId, out var delegatedAdminId))
        {
            return false;
        }

        var delegations = await _delegationRepository.GetActiveAsync(tenantId, cancellationToken);
        return delegations.Any(delegation =>
            delegation.Props.DelegatedAdminId.GetValue() == delegatedAdminId &&
            delegation.CanExecuteAction(DelegatedAction.AssignProfile, entity.RequestedBranchId?.GetValue()));
    }

    private async Task<Result<(Profile Profile, bool IsNew)>> ResolveOrCreateProfileAsync(
        ApprovalRequest entity,
        UserAccount targetUser,
        RoleId grantedRoleId,
        CancellationToken cancellationToken)
    {
        var requestedBranchId = entity.RequestedBranchId?.GetValue();
        var existingProfiles = await _profileRepository.GetByUserIdAsync(targetUser.GetId().GetValue(), cancellationToken);
        var existingProfile = existingProfiles.FirstOrDefault(profile =>
            profile.IsActive &&
            profile.RoleId.GetValue() == grantedRoleId.GetValue() &&
            profile.BranchId?.GetValue() == requestedBranchId);

        if (existingProfile is not null)
        {
            return Result<(Profile Profile, bool IsNew)>.Success((existingProfile, false));
        }

        // G-160: validar que el rol concedido EXISTE antes de materializar el perfil. Sin esta guarda,
        // aprobar con un `grantedRoleId` arbitrario dejaba un Profile con rol FANTASMA que luego rompía
        // la construcción del grafo de autorización del usuario objetivo en el login (401 AUTH_000).
        var grantedRole = await _roleRepository.GetByIdAsync(grantedRoleId.GetValue(), cancellationToken);
        if (grantedRole is null)
        {
            return Result<(Profile Profile, bool IsNew)>.Failure(
                "El rol concedido no existe.");
        }

        var profileResult = Profile.Create(
            targetUser.TenantId,
            UserId.Load(targetUser.GetId().GetValue()),
            grantedRoleId,
            entity.RequestedBranchId is null ? null : BranchId.Load(entity.RequestedBranchId.GetValue()),
            ActorId.Create(_userContext.UserId));

        if (profileResult.IsFailure)
        {
            return Result<(Profile Profile, bool IsNew)>.Failure(profileResult.Error);
        }

        return Result<(Profile Profile, bool IsNew)>.Success((profileResult.Value, true));
    }
}
