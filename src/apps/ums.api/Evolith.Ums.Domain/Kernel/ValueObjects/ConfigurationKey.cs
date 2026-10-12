namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ConfigurationKey : StringValueObject
{
    private ConfigurationKey(string value) : base(value) { }

    public static ConfigurationKey Create(string value) => new ConfigurationKey(value?.Trim() ?? string.Empty);

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new ConfigurationKeyValidator(this));
    }
}

public partial class ConfigurationKeyValidator : AbstractRuleValidator<ValueObject<string>>
{
    public ConfigurationKeyValidator(ValueObject<string> subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        var value = Subject.GetValue();

        if (string.IsNullOrWhiteSpace(value))
        {
            AddBrokenRule(nameof(ConfigurationKey), DomainErrors.ValueObject.PropertyRequired);
            return;
        }

        if (value.Length > 100)
        {
            AddBrokenRule(nameof(ConfigurationKey), DomainErrors.SystemSuite.ConfigurationKeyTooLong);
        }
    }
}
