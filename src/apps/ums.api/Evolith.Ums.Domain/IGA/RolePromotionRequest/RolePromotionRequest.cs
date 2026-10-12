namespace Evolith.Ums.Domain.IGA.RolePromotionRequest;

using Evolith.Ums.Domain.Events;
using Evolith.Ums.Domain.IGA.RolePromotionRequest.Events;

/// <summary>
/// Agregado raíz de promoción de rol del contexto acotado IGA (ADR-UMS-093, FR-060/061).
/// Orquesta la máquina de estados auditada, congela el <see cref="RiskScore"/> al salir de
/// <c>Draft</c> y hace cumplir la segregación de funciones (SoD) en el dominio.
///
/// Máquina de estados:
///   Draft → PendingEligibilityCheck → PendingManagerApproval →
///   (PendingSecurityReview si RiskScore ≥ umbral) → Approved → Executed → Verified.
///   Cortes terminales: Rejected (no elegible / rechazo de gerente o seguridad), Cancelled.
///
/// Invariantes:
///   INV-RPR1 — cada transición valida el estado origen.
///   INV-RPR2 — el RiskScore es inmutable tras congelarse en <c>Submit</c>.
///   INV-RPR3 — SoD (ADR-UMS-096): solicitante ≠ objetivo; aprobador ≠ objetivo ≠ solicitante;
///              revisor ≠ objetivo ≠ aprobador; ejecutor ≠ objetivo ≠ aprobador ≠ revisor;
///              verificador ≠ ejecutor ≠ objetivo.
///   INV-RPR4 — elegibilidad fail-closed: no elegible ⇒ Rejected (nunca avanza).
///   INV-RPR5 — sólo Approved puede Execute; sólo Executed puede Verify.
///
/// Toda operación devuelve <see cref="Result"/> (nunca excepción) y emite un evento
/// de dominio auditable por transición.
/// </summary>
public sealed class RolePromotionRequest : AggregateRoot<RolePromotionRequest, RolePromotionRequestProps>
{
    /// <summary>Umbral de alto riesgo por defecto (ADR-UMS-093). Configurable por la capa de aplicación.</summary>
    public const int DefaultHighRiskThreshold = 70;

    public new RolePromotionRequestEventsManager DomainEvents { get; }

    private RolePromotionRequest(RolePromotionRequestProps props) : base(props)
    {
        DomainEvents = new RolePromotionRequestEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new RolePromotionRequestedEvent(
                Props.TenantId.GetValue(),
                Props.Id.GetValue(),
                Props.TargetUserId.GetValue(),
                Props.RequesterId.GetValue(),
                Props.CurrentRoleId.GetValue(),
                Props.TargetRoleId.GetValue()));
        }
    }

    public RolePromotionRequestId GetId() => RolePromotionRequestId.Load(Props.Id.GetValue());
    public TenantId TenantId => Props.TenantId;
    public UserId TargetUserId => Props.TargetUserId;
    public UserId RequesterId => Props.RequesterId;
    public RoleId CurrentRoleId => Props.CurrentRoleId;
    public RoleId TargetRoleId => Props.TargetRoleId;
    public RolePromotionStatus Status => Props.Status;
    public RiskScore? RiskScore => Props.RiskScore;
    public UserId? ApproverId => Props.ApproverId;
    public UserId? SecurityReviewerId => Props.SecurityReviewerId;
    public UserId? ExecutorId => Props.ExecutorId;
    public UserId? VerifierId => Props.VerifierId;
    public string? DecisionReason => Props.DecisionReason;

    public static Result<RolePromotionRequest> Create(
        TenantId tenantId,
        UserId targetUserId,
        UserId requesterId,
        RoleId currentRoleId,
        RoleId targetRoleId,
        ActorId createdBy)
    {
        var props = new RolePromotionRequestProps(
            RolePromotionRequestId.Create(),
            tenantId,
            targetUserId,
            requesterId,
            currentRoleId,
            targetRoleId,
            createdBy);

        var request = new RolePromotionRequest(props);

        // INV-RPR3: el solicitante no puede promover(se) a sí mismo (SoD).
        if (requesterId.GetValue() == targetUserId.GetValue())
        {
            request.BrokenRules.Add(new BrokenRule(nameof(RequesterId), DomainErrors.IGA.SelfPromotionNotAllowed));
        }

        // Guarda de coherencia: el rol objetivo debe diferir del rol actual.
        if (currentRoleId.GetValue() == targetRoleId.GetValue())
        {
            request.BrokenRules.Add(new BrokenRule(nameof(TargetRoleId), DomainErrors.IGA.SameRolePromotion));
        }

        if (!request.IsValid())
        {
            return Result<RolePromotionRequest>.Failure(request.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<RolePromotionRequest>.Success(request);
    }

    /// <summary>
    /// Draft → PendingEligibilityCheck. Congela el <see cref="RiskScore"/> (INV-RPR2).
    /// Reintentar tras congelar falla con <see cref="DomainErrors.IGA.RiskScoreAlreadyFrozen"/>.
    /// </summary>
    public Result Submit(RiskScore riskScore, ActorId actorId)
    {
        // INV-RPR2: el RiskScore sólo se congela una vez.
        if (Props.RiskScore is not null)
        {
            BrokenRules.Add(new BrokenRule(nameof(RiskScore), DomainErrors.IGA.RiskScoreAlreadyFrozen));
        }

        // INV-RPR1: estado origen válido.
        if (Props.Status != RolePromotionStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithFrozenRiskScore(riskScore));
        DomainEvents.RaiseEvent(new RolePromotionSubmittedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), riskScore.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// INV-RPR4 (fail-closed): PendingEligibilityCheck → PendingManagerApproval si elegible;
    /// si no elegible ⇒ Rejected (la promoción nunca avanza).
    /// </summary>
    public Result ConfirmEligibility(bool isEligible, ActorId actorId)
    {
        // INV-RPR1: estado origen válido.
        if (Props.Status != RolePromotionStatus.PendingEligibilityCheck)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        if (!isEligible)
        {
            SetProps(Props.WithRejection(DomainErrors.IGA.NotEligibleForPromotion));
            DomainEvents.RaiseEvent(new RolePromotionRejectedEvent(
                Props.TenantId.GetValue(), Props.Id.GetValue(), nameof(RolePromotionStatus.PendingEligibilityCheck), DomainErrors.IGA.NotEligibleForPromotion));
            TrackingState.MarkAsDirty();
            Props.Audit.Update(actorId.GetValue());
            return Result.Success();
        }

        SetProps(Props.WithStatus(RolePromotionStatus.PendingManagerApproval));
        DomainEvents.RaiseEvent(new RolePromotionEligibilityConfirmedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// PendingManagerApproval → PendingSecurityReview (RiskScore ≥ umbral) o → Approved (RiskScore &lt; umbral).
    /// SoD (INV-RPR3): aprobador ≠ objetivo y aprobador ≠ solicitante.
    /// </summary>
    public Result ManagerApprove(UserId approverId, ActorId actorId, int highRiskThreshold = DefaultHighRiskThreshold)
    {
        if (Props.Status != RolePromotionStatus.PendingManagerApproval)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (approverId.GetValue() == Props.TargetUserId.GetValue()
            || approverId.GetValue() == Props.RequesterId.GetValue())
        {
            BrokenRules.Add(new BrokenRule(nameof(ApproverId), DomainErrors.IGA.SegregationOfDutiesViolation));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        var score = Props.RiskScore?.GetValue() ?? 0;
        var nextState = score >= highRiskThreshold
            ? RolePromotionStatus.PendingSecurityReview
            : RolePromotionStatus.Approved;

        SetProps(Props.WithApprover(approverId, nextState));
        DomainEvents.RaiseEvent(new RolePromotionManagerApprovedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), approverId.GetValue(), score, nextState.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>PendingManagerApproval → Rejected (el gerente rechaza).</summary>
    public Result ManagerReject(UserId approverId, string reason, ActorId actorId)
    {
        if (Props.Status != RolePromotionStatus.PendingManagerApproval)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (approverId.GetValue() == Props.TargetUserId.GetValue()
            || approverId.GetValue() == Props.RequesterId.GetValue())
        {
            BrokenRules.Add(new BrokenRule(nameof(ApproverId), DomainErrors.IGA.SegregationOfDutiesViolation));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            BrokenRules.Add(new BrokenRule(nameof(DecisionReason), DomainErrors.IGA.DecisionReasonRequired));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithRejection(reason));
        DomainEvents.RaiseEvent(new RolePromotionRejectedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), nameof(RolePromotionStatus.PendingManagerApproval), reason));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// PendingSecurityReview → Approved. SoD (INV-RPR3): revisor ≠ objetivo y revisor ≠ aprobador.
    /// </summary>
    public Result SecurityApprove(UserId reviewerId, ActorId actorId)
    {
        if (Props.Status != RolePromotionStatus.PendingSecurityReview)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (reviewerId.GetValue() == Props.TargetUserId.GetValue()
            || (Props.ApproverId is not null && reviewerId.GetValue() == Props.ApproverId.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(SecurityReviewerId), DomainErrors.IGA.SegregationOfDutiesViolation));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithSecurityReviewer(reviewerId, RolePromotionStatus.Approved));
        DomainEvents.RaiseEvent(new RolePromotionSecurityApprovedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), reviewerId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>PendingSecurityReview → Rejected (seguridad rechaza).</summary>
    public Result SecurityReject(UserId reviewerId, string reason, ActorId actorId)
    {
        if (Props.Status != RolePromotionStatus.PendingSecurityReview)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (reviewerId.GetValue() == Props.TargetUserId.GetValue()
            || (Props.ApproverId is not null && reviewerId.GetValue() == Props.ApproverId.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(SecurityReviewerId), DomainErrors.IGA.SegregationOfDutiesViolation));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            BrokenRules.Add(new BrokenRule(nameof(DecisionReason), DomainErrors.IGA.DecisionReasonRequired));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithRejection(reason));
        DomainEvents.RaiseEvent(new RolePromotionRejectedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), nameof(RolePromotionStatus.PendingSecurityReview), reason));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// INV-RPR5: sólo Approved → Executed (aplica el cambio de rol en Autorización).
    /// SoD (INV-RPR3 endurecida, ADR-UMS-096): ejecutor ≠ objetivo ≠ aprobador ≠ revisor.
    /// Cerrar aquí el hueco impide que quien autoriza (aprobador/revisor) aprovisione su
    /// propia decisión, colapsando la separación autorizar↔ejecutar. Espejo de <see cref="Verify"/>.
    /// </summary>
    public Result Execute(UserId executorId, ActorId actorId)
    {
        if (Props.Status != RolePromotionStatus.Approved)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (executorId.GetValue() == Props.TargetUserId.GetValue()
            || (Props.ApproverId is not null && executorId.GetValue() == Props.ApproverId.GetValue())
            || (Props.SecurityReviewerId is not null && executorId.GetValue() == Props.SecurityReviewerId.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(ExecutorId), DomainErrors.IGA.SegregationOfDutiesViolation));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithExecutor(executorId));
        DomainEvents.RaiseEvent(new RolePromotionExecutedEvent(
            Props.TenantId.GetValue(),
            Props.Id.GetValue(),
            Props.TargetUserId.GetValue(),
            Props.CurrentRoleId.GetValue(),
            Props.TargetRoleId.GetValue(),
            executorId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>
    /// INV-RPR5: sólo Executed → Verified. SoD (INV-RPR3): verificador ≠ ejecutor y verificador ≠ objetivo.
    /// </summary>
    public Result Verify(UserId verifierId, ActorId actorId)
    {
        if (Props.Status != RolePromotionStatus.Executed)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (verifierId.GetValue() == Props.TargetUserId.GetValue()
            || (Props.ExecutorId is not null && verifierId.GetValue() == Props.ExecutorId.GetValue()))
        {
            BrokenRules.Add(new BrokenRule(nameof(VerifierId), DomainErrors.IGA.SegregationOfDutiesViolation));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithVerifier(verifierId));
        DomainEvents.RaiseEvent(new RolePromotionVerifiedEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), verifierId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }

    /// <summary>Draft → Cancelled (el solicitante cancela antes de enviar).</summary>
    public Result Cancel(string reason, ActorId actorId)
    {
        if (Props.Status != RolePromotionStatus.Draft)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.IGA.InvalidStateTransition));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            BrokenRules.Add(new BrokenRule(nameof(DecisionReason), DomainErrors.IGA.DecisionReasonRequired));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithCancellation(reason));
        DomainEvents.RaiseEvent(new RolePromotionCancelledEvent(
            Props.TenantId.GetValue(), Props.Id.GetValue(), reason));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actorId.GetValue());
        return Result.Success();
    }
}
