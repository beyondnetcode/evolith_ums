namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ConfigurationValue : StringValueObject
{
    private ConfigurationValue(string value) : base(value) { }

    public static ConfigurationValue Create(string value) => new ConfigurationValue(value?.Trim() ?? string.Empty);

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new ConfigurationValueValidator(this));
    }
}

public partial class ConfigurationValueValidator : AbstractRuleValidator<ValueObject<string>>
{
    public ConfigurationValueValidator(ValueObject<string> subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        var value = Subject.GetValue();

        if (string.IsNullOrWhiteSpace(value))
        {
            AddBrokenRule(nameof(ConfigurationValue), DomainErrors.ValueObject.PropertyRequired);
            return;
        }

        if (value.Length > 2000)
        {
            AddBrokenRule(nameof(ConfigurationValue), DomainErrors.SystemSuite.ConfigurationValueTooLong);
        }
    }
}
