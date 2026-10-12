namespace Evolith.Ums.Domain.Authorization.AssignmentRule;

using Evolith.Ums.Domain.Authorization.AssignmentRule.Events;
using Evolith.Ums.Domain.Events;

public sealed class TemplateAssignmentRule : AggregateRoot<TemplateAssignmentRule, TemplateAssignmentRuleProps>
{
    public new TemplateAssignmentRuleDomainEventsManager DomainEvents { get; }

    private TemplateAssignmentRule(TemplateAssignmentRuleProps props) : base(props)
    {
        DomainEvents = new TemplateAssignmentRuleDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new AssignmentRuleCreatedEvent(
                Props.Id.GetValue(),
                Props.TenantId.GetValue(),
                Props.TemplateId.GetValue(),
                Props.RoleId.GetValue(),
                Props.Priority));
        }
    }

    public TenantId TenantId => Props.TenantId;
    public TemplateId TemplateId => Props.TemplateId;
    public RoleId RoleId => Props.RoleId;
    public int Priority => Props.Priority;
    public TemplateAssignmentRuleStatus Status => Props.Status;
    public bool IsActive => Props.Status == TemplateAssignmentRuleStatus.Active;

    public TemplateAssignmentRuleId GetId() => TemplateAssignmentRuleId.Load(Props.Id.GetValue());

    public static Result<TemplateAssignmentRule> Create(
        TenantId tenantId,
        TemplateId templateId,
        RoleId roleId,
        int priority,
        ActorId createdBy)
    {
        if (priority <= 0)
        {
            return Result<TemplateAssignmentRule>.Failure(DomainErrors.Authorization.AssignmentRulePriorityMustBePositive);
        }

        var props = new TemplateAssignmentRuleProps(
            IdValueObject.Create(),
            tenantId,
            templateId,
            roleId,
            priority,
            createdBy);

        var rule = new TemplateAssignmentRule(props);

        if (!rule.IsValid())
        {
            return Result<TemplateAssignmentRule>.Failure(rule.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<TemplateAssignmentRule>.Success(rule);
    }

    public Result Deactivate(ActorId updatedBy)
    {
        if (Status == TemplateAssignmentRuleStatus.Inactive)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Authorization.AssignmentRuleAlreadyInactive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(TemplateAssignmentRuleStatus.Inactive));
        DomainEvents.RaiseEvent(new AssignmentRuleDeactivatedEvent(Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Reactivate(ActorId updatedBy)
    {
        if (Status == TemplateAssignmentRuleStatus.Active)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Authorization.AssignmentRuleAlreadyActive));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        SetProps(Props.WithStatus(TemplateAssignmentRuleStatus.Active));
        DomainEvents.RaiseEvent(new AssignmentRuleReactivatedEvent(Props.Id.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }
}
