namespace Evolith.Ums.Domain.Test.IGA;

using Evolith.Ums.Domain.IGA.RolePromotionRequest;
using Xunit;

/// <summary>
/// Pruebas de dominio del agregado <see cref="RolePromotionRequest"/> (ADR-UMS-093, IGA).
///
/// Cobertura: una prueba por transición de la máquina de estados Y por guarda:
///   – estado origen inválido (INV-RPR1) y saltos de estado ilegales;
///   – RiskScore inmutable tras congelar (INV-RPR2);
///   – segregación de funciones violada (INV-RPR3);
///   – elegibilidad fail-closed (INV-RPR4);
///   – sólo Approved→Execute y sólo Executed→Verify (INV-RPR5);
///   – enrutamiento por umbral de riesgo (alto ⇒ revisión de seguridad).
/// Cada transición emite su evento de dominio, también verificado.
/// </summary>
public class RolePromotionRequestTests
{
    private static readonly TenantId Tenant = TenantId.Create();
    private static readonly UserId Target = UserId.Create();
    private static readonly UserId Requester = UserId.Create();
    private static readonly UserId Approver = UserId.Create();
    private static readonly UserId Reviewer = UserId.Create();
    private static readonly UserId Executor = UserId.Create();
    private static readonly UserId Verifier = UserId.Create();
    private static readonly RoleId CurrentRole = RoleId.Create();
    private static readonly RoleId TargetRole = RoleId.Create();
    private static readonly ActorId Actor = ActorId.Create("actor-001");

    private const int LowRisk = 30;
    private const int HighRisk = 85;

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static RolePromotionRequest MakeDraft()
        => RolePromotionRequest.Create(Tenant, Target, Requester, CurrentRole, TargetRole, Actor).Value;

    private static RolePromotionRequest MakeSubmitted(int risk = LowRisk)
    {
        var r = MakeDraft();
        r.Submit(RiskScore.Create(risk).Value, Actor);
        return r;
    }

    private static RolePromotionRequest MakePendingManagerApproval(int risk = LowRisk)
    {
        var r = MakeSubmitted(risk);
        r.ConfirmEligibility(true, Actor);
        return r;
    }

    private static RolePromotionRequest MakeApprovedLowRisk()
    {
        var r = MakePendingManagerApproval(LowRisk);
        r.ManagerApprove(Approver, Actor);
        return r;
    }

    private static RolePromotionRequest MakeApprovedHighRisk()
    {
        var r = MakePendingManagerApproval(HighRisk);
        r.ManagerApprove(Approver, Actor);   // → PendingSecurityReview
        r.SecurityApprove(Reviewer, Actor);  // → Approved (revisor registrado en Props)
        return r;
    }

    private static RolePromotionRequest MakeExecuted()
    {
        var r = MakeApprovedLowRisk();
        r.Execute(Executor, Actor);
        return r;
    }

    // ── Create + SoD (INV-RPR3) ──────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_StartsInDraft()
    {
        var result = RolePromotionRequest.Create(Tenant, Target, Requester, CurrentRole, TargetRole, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Draft, result.Value.Status);
        Assert.Null(result.Value.RiskScore);
        Assert.Contains(result.Value.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionRequestedEvent);
    }

    [Fact]
    public void Create_WhenRequesterEqualsTarget_ReturnsFailure()
    {
        var result = RolePromotionRequest.Create(Tenant, Target, Target, CurrentRole, TargetRole, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SelfPromotionNotAllowed, result.Error);
    }

    [Fact]
    public void Create_WhenTargetRoleEqualsCurrentRole_ReturnsFailure()
    {
        var result = RolePromotionRequest.Create(Tenant, Target, Requester, CurrentRole, CurrentRole, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SameRolePromotion, result.Error);
    }

    // ── Submit (Draft → PendingEligibilityCheck) + RiskScore congelado ────────

    [Fact]
    public void Submit_FromDraft_FreezesRiskScoreAndTransitions()
    {
        var r = MakeDraft();

        var result = r.Submit(RiskScore.Create(55).Value, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.PendingEligibilityCheck, r.Status);
        Assert.Equal(55, r.RiskScore!.GetValue());
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionSubmittedEvent);
    }

    [Fact]
    public void Submit_WhenNotDraft_ReturnsInvalidStateTransition()
    {
        var r = MakePendingManagerApproval();

        var result = r.Submit(RiskScore.Create(10).Value, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InvalidStateTransition, result.Error);
    }

    [Fact]
    public void Submit_WhenRiskScoreAlreadyFrozen_ReturnsFailure()
    {
        // INV-RPR2: reintentar Submit sobre una solicitud ya congelada falla.
        var r = MakeSubmitted(40);

        var result = r.Submit(RiskScore.Create(90).Value, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.RiskScoreAlreadyFrozen, result.Error);
        Assert.Equal(40, r.RiskScore!.GetValue()); // valor original intacto
    }

    [Fact]
    public void RiskScore_RemainsImmutableThroughLifecycle()
    {
        var r = MakeSubmitted(65);
        var frozen = r.RiskScore!.GetValue();

        r.ConfirmEligibility(true, Actor);
        r.ManagerApprove(Approver, Actor);

        Assert.Equal(frozen, r.RiskScore!.GetValue());
    }

    // ── ConfirmEligibility (INV-RPR4 fail-closed) ─────────────────────────────

    [Fact]
    public void ConfirmEligibility_WhenEligible_TransitionsToPendingManagerApproval()
    {
        var r = MakeSubmitted();

        var result = r.ConfirmEligibility(true, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.PendingManagerApproval, r.Status);
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionEligibilityConfirmedEvent);
    }

    [Fact]
    public void ConfirmEligibility_WhenNotEligible_TransitionsToRejected()
    {
        var r = MakeSubmitted();

        var result = r.ConfirmEligibility(false, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Rejected, r.Status);
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionRejectedEvent);
    }

    [Fact]
    public void ConfirmEligibility_WhenNotInEligibilityCheck_ReturnsInvalidStateTransition()
    {
        var r = MakeDraft();

        var result = r.ConfirmEligibility(true, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InvalidStateTransition, result.Error);
    }

    // ── ManagerApprove (enrutamiento por riesgo + SoD) ────────────────────────

    [Fact]
    public void ManagerApprove_WithLowRisk_TransitionsToApproved()
    {
        var r = MakePendingManagerApproval(LowRisk);

        var result = r.ManagerApprove(Approver, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Approved, r.Status);
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionManagerApprovedEvent);
    }

    [Fact]
    public void ManagerApprove_WithHighRisk_TransitionsToPendingSecurityReview()
    {
        var r = MakePendingManagerApproval(HighRisk);

        var result = r.ManagerApprove(Approver, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.PendingSecurityReview, r.Status);
    }

    [Fact]
    public void ManagerApprove_WhenApproverIsTarget_ReturnsSoDViolation()
    {
        var r = MakePendingManagerApproval();

        var result = r.ManagerApprove(Target, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
    }

    [Fact]
    public void ManagerApprove_WhenApproverIsRequester_ReturnsSoDViolation()
    {
        var r = MakePendingManagerApproval();

        var result = r.ManagerApprove(Requester, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
    }

    [Fact]
    public void ManagerApprove_WhenNotPendingManagerApproval_ReturnsInvalidStateTransition()
    {
        var r = MakeSubmitted(); // aún en PendingEligibilityCheck

        var result = r.ManagerApprove(Approver, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InvalidStateTransition, result.Error);
    }

    [Fact]
    public void ManagerReject_FromPendingManagerApproval_TransitionsToRejected()
    {
        var r = MakePendingManagerApproval();

        var result = r.ManagerReject(Approver, "no cumple el perfil", Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Rejected, r.Status);
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionRejectedEvent);
    }

    [Fact]
    public void ManagerReject_WithoutReason_ReturnsFailure()
    {
        var r = MakePendingManagerApproval();

        var result = r.ManagerReject(Approver, "  ", Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.DecisionReasonRequired, result.Error);
    }

    // ── SecurityApprove / SecurityReject (SoD reviewer ≠ approver) ─────────────

    [Fact]
    public void SecurityApprove_FromPendingSecurityReview_TransitionsToApproved()
    {
        var r = MakePendingManagerApproval(HighRisk);
        r.ManagerApprove(Approver, Actor);

        var result = r.SecurityApprove(Reviewer, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Approved, r.Status);
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionSecurityApprovedEvent);
    }

    [Fact]
    public void SecurityApprove_WhenReviewerIsApprover_ReturnsSoDViolation()
    {
        var r = MakePendingManagerApproval(HighRisk);
        r.ManagerApprove(Approver, Actor);

        var result = r.SecurityApprove(Approver, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
    }

    [Fact]
    public void SecurityApprove_WhenReviewerIsTarget_ReturnsSoDViolation()
    {
        var r = MakePendingManagerApproval(HighRisk);
        r.ManagerApprove(Approver, Actor);

        var result = r.SecurityApprove(Target, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
    }

    [Fact]
    public void SecurityApprove_WhenNotInSecurityReview_ReturnsInvalidStateTransition()
    {
        var r = MakeApprovedLowRisk(); // ya Approved, sin pasar por seguridad

        var result = r.SecurityApprove(Reviewer, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InvalidStateTransition, result.Error);
    }

    [Fact]
    public void SecurityReject_FromPendingSecurityReview_TransitionsToRejected()
    {
        var r = MakePendingManagerApproval(HighRisk);
        r.ManagerApprove(Approver, Actor);

        var result = r.SecurityReject(Reviewer, "riesgo inaceptable", Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Rejected, r.Status);
    }

    // ── Execute (INV-RPR5: sólo Approved) ─────────────────────────────────────

    [Fact]
    public void Execute_FromApproved_TransitionsToExecuted()
    {
        var r = MakeApprovedLowRisk();

        var result = r.Execute(Executor, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Executed, r.Status);
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionExecutedEvent);
    }

    [Fact]
    public void Execute_WhenNotApproved_ReturnsInvalidStateTransition()
    {
        var r = MakePendingManagerApproval();

        var result = r.Execute(Executor, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InvalidStateTransition, result.Error);
    }

    // SoD del ejecutor (ADR-UMS-096, INV-RPR3 endurecida): ejecutor ≠ objetivo ≠ aprobador ≠ revisor.

    [Fact]
    public void Execute_WhenExecutorIsApprover_ReturnsSoDViolation()
    {
        var r = MakeApprovedLowRisk(); // aprobado por Approver

        var result = r.Execute(Approver, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
        Assert.Equal(RolePromotionStatus.Approved, r.Status); // no transiciona
    }

    [Fact]
    public void Execute_WhenExecutorIsSecurityReviewer_ReturnsSoDViolation()
    {
        var r = MakeApprovedHighRisk(); // revisado por Reviewer

        var result = r.Execute(Reviewer, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
        Assert.Equal(RolePromotionStatus.Approved, r.Status); // no transiciona
    }

    [Fact]
    public void Execute_WhenExecutorIsTarget_ReturnsSoDViolation()
    {
        var r = MakeApprovedLowRisk();

        var result = r.Execute(Target, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
    }

    // ── Verify (INV-RPR5: sólo Executed + SoD verifier ≠ executor/target) ─────

    [Fact]
    public void Verify_FromExecuted_TransitionsToVerified()
    {
        var r = MakeExecuted();

        var result = r.Verify(Verifier, Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Verified, r.Status);
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionVerifiedEvent);
    }

    [Fact]
    public void Verify_WhenVerifierIsExecutor_ReturnsSoDViolation()
    {
        var r = MakeExecuted();

        var result = r.Verify(Executor, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
    }

    [Fact]
    public void Verify_WhenVerifierIsTarget_ReturnsSoDViolation()
    {
        var r = MakeExecuted();

        var result = r.Verify(Target, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.SegregationOfDutiesViolation, result.Error);
    }

    [Fact]
    public void Verify_WhenNotExecuted_ReturnsInvalidStateTransition()
    {
        var r = MakeApprovedLowRisk();

        var result = r.Verify(Verifier, Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InvalidStateTransition, result.Error);
    }

    // ── Cancel (sólo Draft) ───────────────────────────────────────────────────

    [Fact]
    public void Cancel_FromDraft_TransitionsToCancelled()
    {
        var r = MakeDraft();

        var result = r.Cancel("ya no se requiere", Actor);

        Assert.True(result.IsSuccess);
        Assert.Equal(RolePromotionStatus.Cancelled, r.Status);
        Assert.Contains(r.DomainEvents.GetUncommittedChanges(), e => e is RolePromotionCancelledEvent);
    }

    [Fact]
    public void Cancel_WhenNotDraft_ReturnsInvalidStateTransition()
    {
        var r = MakeSubmitted();

        var result = r.Cancel("tarde", Actor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.IGA.InvalidStateTransition, result.Error);
    }

    // ── Saltos de estado ilegales ─────────────────────────────────────────────

    [Fact]
    public void Execute_DirectlyFromDraft_IsIllegal()
    {
        var r = MakeDraft();

        Assert.True(r.Execute(Executor, Actor).IsFailure);
    }

    [Fact]
    public void Verify_DirectlyFromDraft_IsIllegal()
    {
        var r = MakeDraft();

        Assert.True(r.Verify(Verifier, Actor).IsFailure);
    }

    [Fact]
    public void Verified_IsTerminal_CannotBeExecutedAgain()
    {
        var r = MakeExecuted();
        r.Verify(Verifier, Actor);

        Assert.True(r.Execute(Executor, Actor).IsFailure);
        Assert.Equal(RolePromotionStatus.Verified, r.Status);
    }

    [Fact]
    public void Rejected_IsTerminal_CannotBeApproved()
    {
        var r = MakePendingManagerApproval();
        r.ManagerReject(Approver, "rechazada", Actor);

        Assert.True(r.ManagerApprove(Approver, Actor).IsFailure);
        Assert.Equal(RolePromotionStatus.Rejected, r.Status);
    }
}
