namespace Evolith.Ums.Domain.Configuration.FeatureFlag;

using Evolith.Ums.Domain.Configuration.FeatureFlag.Criteria;
using Evolith.Ums.Domain.Configuration.FeatureFlag.Events;
using Evolith.Ums.Domain.Configuration.FeatureFlag.FlagEvaluationLog;
using FlagEvaluationLogEntity = Evolith.Ums.Domain.Configuration.FeatureFlag.FlagEvaluationLog.FlagEvaluationLog;
using FeatureFlagCriteriaEntity = Evolith.Ums.Domain.Configuration.FeatureFlag.Criteria.FeatureFlagCriteria;

public sealed class FeatureFlag : AggregateRoot<FeatureFlag, FeatureFlagProps>
{
    private readonly List<FlagEvaluationLogEntity> _evaluationLog = new();
    private readonly List<FeatureFlagCriteriaEntity> _criteria = new();

    public new FeatureFlagDomainEventsManager DomainEvents { get; }

    private FeatureFlag(FeatureFlagProps props) : base(props)
    {
        DomainEvents = new FeatureFlagDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new FeatureFlagCreatedEvent(
                props.Id.GetValue(), props.FlagCode, props.FlagType.Name, props.SystemSuiteId.GetValue()));
        }
    }

    public string FlagCode => Props.FlagCode;
    public FlagType FlagType => Props.FlagType;
    public FlagStatus Status => Props.Status;
    public int? RolloutPercentage => Props.RolloutPercentage;
    public Guid SystemSuiteId => Props.SystemSuiteId.GetValue();

    public IReadOnlyCollection<FlagEvaluationLogEntity> EvaluationLog => _evaluationLog.AsReadOnly();
    public IReadOnlyCollection<FeatureFlagCriteriaEntity> Criteria => _criteria.AsReadOnly();

    public FeatureFlagId GetId() => FeatureFlagId.Load(Props.Id.GetValue());

    public static Result<FeatureFlag> Create(
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
        if (systemSuiteId is null)
        {
            return Result<FeatureFlag>.Failure(DomainErrors.Configuration.SystemSuiteIdRequired);
        }

        if (flagType == FlagType.Percentage && (rolloutPercentage is null || rolloutPercentage < 0 || rolloutPercentage > 100))
        {
            return Result<FeatureFlag>.Failure(DomainErrors.Configuration.FlagPercentageOutOfRange);
        }

        var props = new FeatureFlagProps(
            IdValueObject.Create(), systemSuiteId, tenantId, flagCode, flagType, flagTargets,
            linkedResourceType, linkedResourceId, rolloutPercentage, createdBy);

        var flag = new FeatureFlag(props);

        if (!flag.IsValid())
        {
            return Result<FeatureFlag>.Failure(flag.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<FeatureFlag>.Success(flag);
    }

    public Result Activate(ActorId updatedBy)
    {
        if (Status == FlagStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.FlagArchivedCannotChange));
        }
        else if (Status == FlagStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.FlagAlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(FlagStatus.Active));
        DomainEvents.RaiseEvent(new FeatureFlagActivatedEvent(Props.Id.GetValue(), Props.FlagCode));
        DomainEvents.RaiseEvent(new FeatureFlagStateChangedEvent(Props.Id.GetValue(), Props.FlagCode, FlagStatus.Active.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy)
    {
        if (Status == FlagStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.FlagArchivedCannotChange));
        }
        else if (Status == FlagStatus.Inactive)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.FlagAlreadyInactive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(FlagStatus.Inactive));
        DomainEvents.RaiseEvent(new FeatureFlagDeactivatedEvent(Props.Id.GetValue(), Props.FlagCode));
        DomainEvents.RaiseEvent(new FeatureFlagStateChangedEvent(Props.Id.GetValue(), Props.FlagCode, FlagStatus.Inactive.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Archive(ActorId updatedBy)
    {
        if (Status == FlagStatus.Archived)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Configuration.FlagArchivedCannotChange));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(FlagStatus.Archived));
        DomainEvents.RaiseEvent(new FeatureFlagArchivedEvent(Props.Id.GetValue(), Props.FlagCode));
        DomainEvents.RaiseEvent(new FeatureFlagStateChangedEvent(Props.Id.GetValue(), Props.FlagCode, FlagStatus.Archived.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result AddCriteria(string criteriaType, string @operator, string value, ActorId updatedBy)
    {
        if (Status == FlagStatus.Archived)
            return Result.Failure(DomainErrors.Configuration.FlagArchivedCannotChange);

        var duplicate = _criteria.Any(c =>
            c.CriteriaType == criteriaType && c.Operator == @operator && c.Value == value);
        if (duplicate)
            return Result.Failure(DomainErrors.Configuration.DuplicateCriteria);

        var criteriaResult = FeatureFlagCriteriaEntity.Create(criteriaType, @operator, value);
        if (criteriaResult.IsFailure)
            return Result.Failure(criteriaResult.Error);

        _criteria.Add(criteriaResult.Value);
        DomainEvents.RaiseEvent(new FeatureFlagCriteriaAddedEvent(Props.Id.GetValue(), Props.FlagCode, criteriaType));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result RemoveCriteria(Guid criteriaId, ActorId updatedBy)
    {
        if (Status == FlagStatus.Archived)
            return Result.Failure(DomainErrors.Configuration.FlagArchivedCannotChange);

        var criteria = _criteria.FirstOrDefault(c => c.Props.Id.GetValue() == criteriaId);
        if (criteria is null)
            return Result.Failure(DomainErrors.Configuration.CriteriaNotFound);

        _criteria.Remove(criteria);
        DomainEvents.RaiseEvent(new FeatureFlagCriteriaRemovedEvent(Props.Id.GetValue(), Props.FlagCode, criteriaId));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result UpdateTargeting(string flagTargets, int? rolloutPercentage, ActorId updatedBy)
    {
        if (Status == FlagStatus.Archived)
            return Result.Failure(DomainErrors.Configuration.FlagArchivedCannotChange);
        if (Props.FlagType == FlagType.Percentage && (rolloutPercentage is null || rolloutPercentage < 0 || rolloutPercentage > 100))
            return Result.Failure(DomainErrors.Configuration.FlagPercentageOutOfRange);

        SetProps(Props.WithFlagTargets(flagTargets).WithRolloutPercentage(rolloutPercentage));
        DomainEvents.RaiseEvent(new FeatureFlagTargetingRulesUpdatedEvent(Props.Id.GetValue(), Props.FlagCode, Props.SystemSuiteId.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result<FlagEvaluationResult> Evaluate(Guid evaluatedBy, EvaluationContext context, IFeatureFlagEvaluator evaluator)
    {
        if (Status == FlagStatus.Archived)
            return Result<FlagEvaluationResult>.Failure(DomainErrors.Configuration.FlagArchivedCannotChange);

        var evalResult = evaluator.Evaluate(this, context);

        _evaluationLog.Add(FlagEvaluationLogEntity.Record(evaluatedBy, evalResult.IsEnabled,
            System.Text.Json.JsonSerializer.Serialize(context)));
        DomainEvents.RaiseEvent(new FlagEvaluatedEvent(Props.Id.GetValue(), Props.FlagCode, evalResult.IsEnabled,
            evalResult.Reason ?? string.Empty));
        return Result<FlagEvaluationResult>.Success(evalResult);
    }
}
