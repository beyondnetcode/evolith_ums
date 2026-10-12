namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class GenericStringValidator : AbstractRuleValidator<ValueObject<string>>
{
    private readonly string _propertyName;
    private readonly bool _isRequired;

    public GenericStringValidator(ValueObject<string> subject, string propertyName, bool isRequired = true) : base(subject)
    {
        _propertyName = propertyName;
        _isRequired = isRequired;
    }

    public override void AddRules(RuleContext? context)
    {
        var value = Subject.GetValue();
        if (_isRequired && string.IsNullOrWhiteSpace(value))
        {
            AddBrokenRule(_propertyName, DomainErrors.ValueObject.PropertyRequired);
        }
    }
}
