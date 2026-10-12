namespace Evolith.Ums.Domain.IGA.RolePromotionRequest;

/// <summary>
/// Estado inmutable (patrón <c>With*</c>) del agregado <see cref="RolePromotionRequest"/>.
/// El <see cref="RiskScore"/> es nulo en <c>Draft</c> y se congela una única vez al hacer
/// <c>Submit</c> (INV-RPR2). Los identificadores de aprobador, revisor, ejecutor y verificador
/// se registran a medida que avanza la máquina de estados para poder validar la SoD (INV-RPR3).
/// </summary>
public class RolePromotionRequestProps : IProps
{
    public RolePromotionRequestId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public UserId TargetUserId { get; private set; }
    public UserId RequesterId { get; private set; }
    public RoleId CurrentRoleId { get; private set; }
    public RoleId TargetRoleId { get; private set; }
    public RolePromotionStatus Status { get; private set; }
    public RiskScore? RiskScore { get; private set; }
    public UserId? ApproverId { get; private set; }
    public UserId? SecurityReviewerId { get; private set; }
    public UserId? ExecutorId { get; private set; }
    public UserId? VerifierId { get; private set; }
    public string? DecisionReason { get; private set; }
    public AuditValueObject Audit { get; private set; }

    /// <summary>Constructor de creación: arranca en <c>Draft</c> sin RiskScore.</summary>
    public RolePromotionRequestProps(
        RolePromotionRequestId id,
        TenantId tenantId,
        UserId targetUserId,
        UserId requesterId,
        RoleId currentRoleId,
        RoleId targetRoleId,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        TargetUserId = targetUserId;
        RequesterId = requesterId;
        CurrentRoleId = currentRoleId;
        TargetRoleId = targetRoleId;
        Status = RolePromotionStatus.Draft;
        RiskScore = null;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    /// <summary>Constructor de rehidratación (persistencia).</summary>
    public RolePromotionRequestProps(
        RolePromotionRequestId id,
        TenantId tenantId,
        UserId targetUserId,
        UserId requesterId,
        RoleId currentRoleId,
        RoleId targetRoleId,
        RolePromotionStatus status,
        RiskScore? riskScore,
        UserId? approverId,
        UserId? securityReviewerId,
        UserId? executorId,
        UserId? verifierId,
        string? decisionReason,
        AuditValueObject audit)
    {
        Id = id;
        TenantId = tenantId;
        TargetUserId = targetUserId;
        RequesterId = requesterId;
        CurrentRoleId = currentRoleId;
        TargetRoleId = targetRoleId;
        Status = status;
        RiskScore = riskScore;
        ApproverId = approverId;
        SecurityReviewerId = securityReviewerId;
        ExecutorId = executorId;
        VerifierId = verifierId;
        DecisionReason = decisionReason;
        Audit = audit;
    }

    public RolePromotionRequestProps WithStatus(RolePromotionStatus status)
    {
        var clone = (RolePromotionRequestProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    /// <summary>Congela el RiskScore y pasa a <c>PendingEligibilityCheck</c> (INV-RPR2).</summary>
    public RolePromotionRequestProps WithFrozenRiskScore(RiskScore riskScore)
    {
        var clone = (RolePromotionRequestProps)MemberwiseClone();
        clone.RiskScore = riskScore;
        clone.Status = RolePromotionStatus.PendingEligibilityCheck;
        return clone;
    }

    public RolePromotionRequestProps WithApprover(UserId approverId, RolePromotionStatus status)
    {
        var clone = (RolePromotionRequestProps)MemberwiseClone();
        clone.ApproverId = approverId;
        clone.Status = status;
        return clone;
    }

    public RolePromotionRequestProps WithSecurityReviewer(UserId reviewerId, RolePromotionStatus status)
    {
        var clone = (RolePromotionRequestProps)MemberwiseClone();
        clone.SecurityReviewerId = reviewerId;
        clone.Status = status;
        return clone;
    }

    public RolePromotionRequestProps WithExecutor(UserId executorId)
    {
        var clone = (RolePromotionRequestProps)MemberwiseClone();
        clone.ExecutorId = executorId;
        clone.Status = RolePromotionStatus.Executed;
        return clone;
    }

    public RolePromotionRequestProps WithVerifier(UserId verifierId)
    {
        var clone = (RolePromotionRequestProps)MemberwiseClone();
        clone.VerifierId = verifierId;
        clone.Status = RolePromotionStatus.Verified;
        return clone;
    }

    public RolePromotionRequestProps WithRejection(string reason)
    {
        var clone = (RolePromotionRequestProps)MemberwiseClone();
        clone.Status = RolePromotionStatus.Rejected;
        clone.DecisionReason = reason;
        return clone;
    }

    public RolePromotionRequestProps WithCancellation(string reason)
    {
        var clone = (RolePromotionRequestProps)MemberwiseClone();
        clone.Status = RolePromotionStatus.Cancelled;
        clone.DecisionReason = reason;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}
