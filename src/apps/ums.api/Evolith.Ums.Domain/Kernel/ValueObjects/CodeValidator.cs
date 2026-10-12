namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class CodeValidator : AbstractRuleValidator<ValueObject<string>>
{
    public CodeValidator(ValueObject<string> subject) : base(subject) { }

    public override void AddRules(RuleContext? context)
    {
        var value = Subject.GetValue();
        if (string.IsNullOrWhiteSpace(value))
        {
            AddBrokenRule("Code", DomainErrors.Common.Required);
        }
    }
}
