namespace Evolith.Ums.Domain.Authorization.Template.PermissionTemplateItem;

using BeyondNetCode.Shell.Ddd.Rules.Impl;
using Evolith.Ums.Domain.Kernel.ValueObjects;

public sealed class PermissionTemplateItem : Entity<PermissionTemplateItem, PermissionTemplateItemProps>
{
    private PermissionTemplateItem(PermissionTemplateItemProps props) : base(props)
    {
        AddValidationRules();
    }

    public TemplateId TemplateId => Props.TemplateId;
    public ExclusiveArcTarget TargetType => Props.TargetType;
    public IdValueObject TargetId => Props.TargetId;
    public ActionId ActionId => Props.ActionId;
    public bool IsAllowed => Props.IsAllowed;
    public bool IsDenied => Props.IsDenied;
    public bool IsActive => Props.IsActive;

    public PermissionTemplateItemId GetId() => PermissionTemplateItemId.Load(Props.Id.GetValue());

    public static Result<PermissionTemplateItem> Create(
        TemplateId templateId,
        ExclusiveArcTarget targetType,
        IdValueObject targetId,
        ActionId actionId,
        bool isAllowed,
        bool isDenied,
        ActorId createdBy)
    {
        var props = new PermissionTemplateItemProps(
            IdValueObject.Create(),
            templateId,
            targetType,
            targetId,
            actionId,
            isAllowed,
            isDenied,
            true,
            createdBy);

        var item = new PermissionTemplateItem(props);

        if (!item.IsValid())
        {
            return Result<PermissionTemplateItem>.Failure(item.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<PermissionTemplateItem>.Success(item);
    }

    public Result SetAllow(ActorId updatedBy)
    {
        Props.IsAllowed = true;
        Props.IsDenied = false;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result SetDeny(ActorId updatedBy)
    {
        Props.IsAllowed = false;
        Props.IsDenied = true;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result SetNeutral(ActorId updatedBy)
    {
        Props.IsAllowed = false;
        Props.IsDenied = false;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Activate(ActorId updatedBy)
    {
        Props.IsActive = true;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    public Result Deactivate(ActorId updatedBy)
    {
        Props.IsActive = false;
        Props.Audit.Update(updatedBy.GetValue());
        return Result.Success();
    }

    private void AddValidationRules()
    {
        ValidatorRules.Add(new ExclusiveArcValidator(this));
        ValidatorRules.Add(new PermissionEffectValidator(this));
    }
}

public class ExclusiveArcValidator : AbstractRuleValidator<PermissionTemplateItem>
{
    public ExclusiveArcValidator(PermissionTemplateItem subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        if (Subject.Props.TargetId is null)
        {
            AddBrokenRule(nameof(PermissionTemplateItem.TargetId), DomainErrors.Common.Required);
        }
    }
}

public class PermissionEffectValidator : AbstractRuleValidator<PermissionTemplateItem>
{
    public PermissionEffectValidator(PermissionTemplateItem subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        if (Subject.Props.IsAllowed && Subject.Props.IsDenied)
        {
            AddBrokenRule(nameof(PermissionTemplateItem), DomainErrors.Authorization.InvalidPermissionEffect);
        }
    }
}
