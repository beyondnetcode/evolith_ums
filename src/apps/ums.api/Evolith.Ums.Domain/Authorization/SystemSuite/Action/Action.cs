namespace Evolith.Ums.Domain.Authorization.SystemSuite.Action;

using BeyondNetCode.Shell.Ddd.Rules.Impl;

public sealed class Action : Entity<Action, ActionProps>
{
    private Action(ActionProps props) : base(props)
    {
        AddValidationRules();
    }

    public TenantId TenantId => Props.TenantId;
    public SystemSuiteId? SystemSuiteId => Props.SystemSuiteId;
    public ModuleId? ModuleId => Props.ModuleId;
    public ActionCode Code => Props.Code;
    public Name Name => Props.Name;

    public ActionId GetId() => ActionId.Load(Props.Id.GetValue());

    public static Result<Action> Create(
        TenantId tenantId,
        SystemSuiteId? systemSuiteId,
        ModuleId? moduleId,
        ActionCode code,
        Name name,
        ActorId createdBy)
    {
        var props = new ActionProps(IdValueObject.Create(), tenantId, systemSuiteId, moduleId, code, name, createdBy);
        var action = new Action(props);

        if (!action.IsValid())
        {
            return Result<Action>.Failure(action.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<Action>.Success(action);
    }

    private void AddValidationRules()
    {
        ValidatorRules.Add(new ActionXorValidator(this));
    }
}

public class ActionXorValidator : AbstractRuleValidator<Action>
{
    public ActionXorValidator(Action subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        var hasSuite = Subject.Props.SystemSuiteId is not null;
        var hasModule = Subject.Props.ModuleId is not null;

        if (!hasSuite && !hasModule)
        {
            AddBrokenRule(nameof(Action), DomainErrors.SystemSuite.ActionRequiresOwner);
        }

        if (hasSuite && hasModule)
        {
            AddBrokenRule(nameof(Action), DomainErrors.SystemSuite.ActionXorViolation);
        }
    }
}
