namespace Evolith.Ums.Domain.Configuration.FeatureFlag;

public class FeatureFlagProps : IProps
{
    public IdValueObject Id { get; private set; }
    public IdValueObject SystemSuiteId { get; private set; }
    public IdValueObject? TenantId { get; private set; }
    public string FlagCode { get; private set; }
    public FlagType FlagType { get; private set; }
    public string FlagTargets { get; private set; }
    public FlagStatus Status { get; private set; }
    public LinkedResourceType? LinkedResourceType { get; private set; }
    public IdValueObject? LinkedResourceId { get; private set; }
    public int? RolloutPercentage { get; private set; }
    public AuditValueObject Audit { get; private set; }

    public FeatureFlagProps(
        IdValueObject id,
        IdValueObject systemSuiteId,
        IdValueObject? tenantId,
        string flagCode,
        FlagType flagType,
        string flagTargets,
        LinkedResourceType? linkedResourceType,
        IdValueObject? linkedResourceId,
        int? rolloutPercentage,
        ActorId createdBy)
    {
        Id = id;
        SystemSuiteId = systemSuiteId;
        TenantId = tenantId;
        FlagCode = flagCode;
        FlagType = flagType;
        FlagTargets = flagTargets;
        Status = FlagStatus.Inactive;
        LinkedResourceType = linkedResourceType;
        LinkedResourceId = linkedResourceId;
        RolloutPercentage = rolloutPercentage;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public FeatureFlagProps(
        IdValueObject id,
        IdValueObject systemSuiteId,
        IdValueObject? tenantId,
        string flagCode,
        FlagType flagType,
        string flagTargets,
        FlagStatus status,
        LinkedResourceType? linkedResourceType,
        IdValueObject? linkedResourceId,
        int? rolloutPercentage,
        AuditValueObject audit)
    {
        Id = id;
        SystemSuiteId = systemSuiteId;
        TenantId = tenantId;
        FlagCode = flagCode;
        FlagType = flagType;
        FlagTargets = flagTargets;
        Status = status;
        LinkedResourceType = linkedResourceType;
        LinkedResourceId = linkedResourceId;
        RolloutPercentage = rolloutPercentage;
        Audit = audit;
    }

    public FeatureFlagProps WithStatus(FlagStatus status)
    {
        var clone = (FeatureFlagProps)MemberwiseClone();
        clone.Status = status;
        return clone;
    }

    public FeatureFlagProps WithFlagTargets(string flagTargets)
    {
        var clone = (FeatureFlagProps)MemberwiseClone();
        clone.FlagTargets = flagTargets;
        return clone;
    }

    public FeatureFlagProps WithRolloutPercentage(int? rolloutPercentage)
    {
        var clone = (FeatureFlagProps)MemberwiseClone();
        clone.RolloutPercentage = rolloutPercentage;
        return clone;
    }

    public object Clone() => MemberwiseClone();
}