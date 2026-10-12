namespace Evolith.Ums.Domain.IGA.RoleMaturityStatus;

/// <summary>
/// Estado inmutable (patrón <c>With*</c>) del agregado <see cref="RoleMaturityStatus"/>.
/// Sigue el estilo de <c>UserManagementDelegationProps</c>: setters privados y clonación
/// mediante <c>MemberwiseClone</c>.
/// </summary>
public class RoleMaturityStatusProps : IProps
{
    public RoleMaturityStatusId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public UserId UserId { get; private set; }
    public RoleId RoleId { get; private set; }
    public RoleMaturityLevel CurrentMaturityLevel { get; private set; }
    public RoleMaturityLevel? NextEligibleMaturityLevel { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime CurrentLevelSince { get; private set; }
    public DateTime? EligibleForPromotionAt { get; private set; }
    public int CompletedCertificationsCount { get; private set; }
    public int CompletedTrainingsCount { get; private set; }
    public decimal PerformanceScore { get; private set; }
    public bool HasNoComplianceIssues { get; private set; }
    public TextValueObject? BlockingFactor { get; private set; }
    public DateTime? LastReviewedAt { get; private set; }
    public AuditValueObject Audit { get; private set; }

    /// <summary>Constructor de creación: nivel inicial, sin incidencias de cumplimiento y desempeño 0.</summary>
    public RoleMaturityStatusProps(
        RoleMaturityStatusId id,
        TenantId tenantId,
        UserId userId,
        RoleId roleId,
        RoleMaturityLevel currentMaturityLevel,
        DateTime assignedAt,
        ActorId createdBy)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
        CurrentMaturityLevel = currentMaturityLevel;
        NextEligibleMaturityLevel = null;
        AssignedAt = assignedAt;
        CurrentLevelSince = assignedAt;
        EligibleForPromotionAt = null;
        CompletedCertificationsCount = 0;
        CompletedTrainingsCount = 0;
        PerformanceScore = 0m;
        HasNoComplianceIssues = true;
        BlockingFactor = null;
        LastReviewedAt = null;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    /// <summary>Constructor de rehidratación (persistencia).</summary>
    public RoleMaturityStatusProps(
        RoleMaturityStatusId id,
        TenantId tenantId,
        UserId userId,
        RoleId roleId,
        RoleMaturityLevel currentMaturityLevel,
        RoleMaturityLevel? nextEligibleMaturityLevel,
        DateTime assignedAt,
        DateTime currentLevelSince,
        DateTime? eligibleForPromotionAt,
        int completedCertificationsCount,
        int completedTrainingsCount,
        decimal performanceScore,
        bool hasNoComplianceIssues,
        TextValueObject? blockingFactor,
        DateTime? lastReviewedAt,
        AuditValueObject audit)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        RoleId = roleId;
        CurrentMaturityLevel = currentMaturityLevel;
        NextEligibleMaturityLevel = nextEligibleMaturityLevel;
        AssignedAt = assignedAt;
        CurrentLevelSince = currentLevelSince;
        EligibleForPromotionAt = eligibleForPromotionAt;
        CompletedCertificationsCount = completedCertificationsCount;
        CompletedTrainingsCount = completedTrainingsCount;
        PerformanceScore = performanceScore;
        HasNoComplianceIssues = hasNoComplianceIssues;
        BlockingFactor = blockingFactor;
        LastReviewedAt = lastReviewedAt;
        Audit = audit;
    }

    public RoleMaturityStatusProps WithPerformanceScore(decimal performanceScore)
    {
        var clone = (RoleMaturityStatusProps)MemberwiseClone();
        clone.PerformanceScore = performanceScore;
        return clone;
    }

    public RoleMaturityStatusProps WithMaturityLevel(RoleMaturityLevel level, DateTime changedAt)
    {
        var clone = (RoleMaturityStatusProps)MemberwiseClone();
        clone.CurrentMaturityLevel = level;
        clone.CurrentLevelSince = changedAt;
        clone.NextEligibleMaturityLevel = null;
        clone.EligibleForPromotionAt = null;
        return clone;
    }

    public RoleMaturityStatusProps WithComplianceIssue(TextValueObject blockingFactor)
    {
        var clone = (RoleMaturityStatusProps)MemberwiseClone();
        clone.HasNoComplianceIssues = false;
        clone.BlockingFactor = blockingFactor;
        return clone;
    }

    public RoleMaturityStatusProps WithComplianceCleared()
    {
        var clone = (RoleMaturityStatusProps)MemberwiseClone();
        clone.HasNoComplianceIssues = true;
        clone.BlockingFactor = null;
        return clone;
    }

    public RoleMaturityStatusProps WithEligibility(RoleMaturityLevel nextLevel, DateTime reviewedAt)
    {
        var clone = (RoleMaturityStatusProps)MemberwiseClone();
        clone.NextEligibleMaturityLevel = nextLevel;
        clone.EligibleForPromotionAt = reviewedAt;
        clone.LastReviewedAt = reviewedAt;
        return clone;
    }

    public RoleMaturityStatusProps WithCertificationRecorded()
    {
        var clone = (RoleMaturityStatusProps)MemberwiseClone();
        clone.CompletedCertificationsCount += 1;
        return clone;
    }

    public RoleMaturityStatusProps WithTrainingRecorded()
    {
        var clone = (RoleMaturityStatusProps)MemberwiseClone();
        clone.CompletedTrainingsCount += 1;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}
