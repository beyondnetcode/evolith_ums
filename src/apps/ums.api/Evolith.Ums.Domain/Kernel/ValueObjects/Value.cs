namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class Value : StringValueObject
{
    private Value(string value) : base(value) { }
    public static Value Create(string value) => new Value(value?.Trim() ?? string.Empty);
    public static Value Default() => new Value(string.Empty);
    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new GenericStringValidator(this, nameof(Value)));
    }
}
