namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ActionCode : StringValueObject
{
    private ActionCode(string value) : base(value) { }

    public static ActionCode Create(string value) => new ActionCode(value?.Trim().ToUpperInvariant() ?? string.Empty);

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new ActionCodeValidator(this));
    }
}

public partial class ActionCodeValidator : AbstractRuleValidator<ValueObject<string>>
{
    public ActionCodeValidator(ValueObject<string> subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        var value = Subject.GetValue();

        if (string.IsNullOrWhiteSpace(value))
        {
            AddBrokenRule(nameof(ActionCode), DomainErrors.ValueObject.PropertyRequired);
            return;
        }

        if (value.Length > 50)
        {
            AddBrokenRule(nameof(ActionCode), DomainErrors.SystemSuite.ActionCodeTooLong);
        }
    }
}
