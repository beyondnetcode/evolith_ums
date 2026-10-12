namespace Evolith.Ums.Domain.Identity.UserManagementDelegation;

using Evolith.Ums.Domain.Events;

public sealed class UserManagementDelegation : AggregateRoot<UserManagementDelegation, UserManagementDelegationProps>
{
    public new UserManagementDelegationEventsManager DomainEvents { get; }

    private UserManagementDelegation(UserManagementDelegationProps props) : base(props)
    {
        DomainEvents = new UserManagementDelegationEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new DelegationCreatedEvent(
                Props.Id.GetValue(),
                Props.TenantId.GetValue(),
                Props.DelegatingAdminId.GetValue(),
                Props.DelegatedAdminId.GetValue(),
                Props.ScopeType.Name,
                string.Join(",", Props.AllowedActions.Select(a => a.Name))));
        }
    }

    public DelegationId GetId() => DelegationId.Load(Props.Id.GetValue());
    public TenantId TenantId => Props.TenantId;
    public UserAccountId DelegatingAdminId => Props.DelegatingAdminId;
    public UserAccountId DelegatedAdminId => Props.DelegatedAdminId;
    public DelegationScopeType ScopeType => Props.ScopeType;
    public Guid? ScopeId => Props.ScopeId;
    public IReadOnlyList<DelegatedAction> AllowedActions => Props.AllowedActions;
    public DateTimeOffset ValidFrom => Props.ValidFrom;
    public DateTimeOffset ValidUntil => Props.ValidUntil;
    public int? MaxDurationDays => Props.MaxDurationDays;
    public bool RequiresApproval => Props.RequiresApproval;
    public Guid? ApprovalRequestId => Props.ApprovalRequestId;
    public DelegationStatus Status => Props.Status;
    public DateTimeOffset? RevokedAt => Props.RevokedAt;
    public Guid? RevokedBy => Props.RevokedBy;
    public string? RevocationReason => Props.RevocationReason;

    public static Result<UserManagementDelegation> Create(
        TenantId tenantId,
        UserAccountId delegatingAdminId,
        UserAccountId delegatedAdminId,
        DelegationScopeType scopeType,
        Guid? scopeId,
        IReadOnlyList<DelegatedAction> allowedActions,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        int? maxDurationDays,
        bool requiresApproval,
        ActorId actorId)
    {
        var id = DelegationId.Create();
        var props = new UserManagementDelegationProps(
            id,
            tenantId,
            delegatingAdminId,
            delegatedAdminId,
            scopeType,
            scopeId,
            allowedActions,
            validFrom,
            validUntil,
            maxDurationDays,
            requiresApproval,
            actorId);

        var delegation = new UserManagementDelegation(props);

        // INV-DEL2: DelegatingAdminId != DelegatedAdminId
        if (delegatingAdminId.GetValue() == delegatedAdminId.GetValue())
        {
            delegation.BrokenRules.Add(new BrokenRule(nameof(DelegatingAdminId), DomainErrors.Delegation.SelfDelegationNotAllowed));
        }

        // INV-DEL3: ValidUntil > ValidFrom
        if (validUntil <= validFrom)
        {
            delegation.BrokenRules.Add(new BrokenRule(nameof(ValidUntil), DomainErrors.Delegation.ValidUntilMustBeAfterValidFrom));
        }

        // INV-DEL4: AllowedActions not empty
        if (allowedActions == null || allowedActions.Count == 0)
        {
            delegation.BrokenRules.Add(new BrokenRule(nameof(AllowedActions), DomainErrors.Delegation.AllowedActionsRequired));
        }

        if (!delegation.IsValid())
        {
            return Result<UserManagementDelegation>.Failure(delegation.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<UserManagementDelegation>.Success(delegation);
    }

    public Result Activate(ActorId actorId)
    {
        // FAIL-CLOSED: si la delegación requiere aprobación, la única vía legítima a Active es
        // SubmitForApproval → Approve. La activación directa queda vedada.
        if (Props.RequiresApproval)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.ApprovalRequired));
        }

        // INV-DEL7: REVOKED/EXPIRED → cannot re-activate
        if (Props.Status == DelegationStatus.Revoked || Props.Status == DelegationStatus.Expired)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotActivateFromCurrentStatus));
        }

        if (Props.Status != DelegationStatus.Draft && Props.Status != DelegationStatus.PendingApproval)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotActivateFromCurrentStatus));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(DelegationStatus.Active));
        DomainEvents.RaiseEvent(new DelegationActivatedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            Props.ValidFrom,
            Props.ValidUntil));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result SubmitForApproval(Guid approvalRequestId, ActorId actorId)
    {
        if (Props.Status != DelegationStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotActivateFromCurrentStatus));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithApprovalRequestId(approvalRequestId).WithStatus(DelegationStatus.PendingApproval));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result Approve(ActorId actorId)
    {
        if (Props.Status != DelegationStatus.PendingApproval)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotActivateFromCurrentStatus));
        }

        // INV-DEL8 (SoD / separación de funciones): el aprobador no puede ser el administrador
        // delegante (quien solicita/crea la delegación). Auto-aprobar una delegación que otorga
        // autoridad de administrador es escalada de privilegios. Invariante de dominio, hecha
        // cumplir aquí (además de la guarda de autorización de la capa de aplicación).
        if (Guid.TryParse(actorId.GetValue(), out var approverId) &&
            approverId == Props.DelegatingAdminId.GetValue())
        {
            BrokenRules.Add(new BrokenRule(nameof(DelegatingAdminId), DomainErrors.Delegation.SelfApprovalNotAllowed));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(DelegationStatus.Active));
        DomainEvents.RaiseEvent(new DelegationActivatedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            Props.ValidFrom,
            Props.ValidUntil));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result Reject(string reason, ActorId actorId)
    {
        if (Props.Status != DelegationStatus.PendingApproval)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotActivateFromCurrentStatus));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            BrokenRules.Add(new BrokenRule(nameof(RevocationReason), DomainErrors.Delegation.RevocationReasonRequired));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(DelegationStatus.Rejected).WithRevocationReason(reason));
        DomainEvents.RaiseEvent(new DelegationRejectedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            reason));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result Revoke(string reason, ActorId actorId)
    {
        if (Props.Status != DelegationStatus.Active && Props.Status != DelegationStatus.PendingApproval)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotRevokeFromCurrentStatus));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            BrokenRules.Add(new BrokenRule(nameof(RevocationReason), DomainErrors.Delegation.RevocationReasonRequired));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var revokedByValue = Guid.TryParse(actorId.GetValue(), out var revokedBy) ? revokedBy : (Guid?)null;
        SetProps(Props.WithStatus(DelegationStatus.Revoked).WithRevokedAt(DateTimeOffset.UtcNow).WithRevokedBy(revokedByValue).WithRevocationReason(reason));
        DomainEvents.RaiseEvent(new DelegationRevokedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            Props.RevokedBy ?? Guid.Empty,
            reason));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result Expire(ActorId actorId)
    {
        if (Props.Status != DelegationStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotActivateFromCurrentStatus));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(DelegationStatus.Expired));
        DomainEvents.RaiseEvent(new DelegationExpiredEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            DateTimeOffset.UtcNow));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result Complete(ActorId actorId)
    {
        if (Props.Status != DelegationStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotActivateFromCurrentStatus));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(DelegationStatus.Completed));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public Result Archive(ActorId actorId)
    {
        var terminalStatuses = new[] { DelegationStatus.Revoked, DelegationStatus.Expired, DelegationStatus.Completed, DelegationStatus.Rejected };
        if (!terminalStatuses.Contains(Props.Status))
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Delegation.CannotArchiveFromCurrentStatus));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var previousStatus = Props.Status.Name;
        SetProps(Props.WithStatus(DelegationStatus.Archived));
        DomainEvents.RaiseEvent(new DelegationArchivedEvent(
            Props.Id.GetValue(),
            Props.TenantId.GetValue(),
            previousStatus));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    public bool CanExecuteAction(DelegatedAction action, Guid? targetScopeId)
    {
        if (Props.Status != DelegationStatus.Active)
        {
            return false;
        }

        if (!Props.AllowedActions.Any(a => a.Id == action.Id))
        {
            return false;
        }

        if (Props.ScopeId.HasValue && targetScopeId.HasValue && Props.ScopeId.Value != targetScopeId.Value)
        {
            return false;
        }

        return true;
    }
}
