namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public partial class Logo : StringValueObject
{
    private Logo(string value) : base(value) { }

    public static Logo Create(string value) => new Logo(value?.Trim() ?? string.Empty);

    public static Logo Default() => new Logo(string.Empty);

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new LogoValueValidator(this));
    }
}

public class LogoValueValidator : AbstractRuleValidator<ValueObject<string>>
{
    public LogoValueValidator(ValueObject<string> subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        var value = Subject.GetValue();

        if (string.IsNullOrWhiteSpace(value))
        {
            AddBrokenRule(nameof(Logo), DomainErrors.ValueObject.PropertyRequired);
        }
    }
}
