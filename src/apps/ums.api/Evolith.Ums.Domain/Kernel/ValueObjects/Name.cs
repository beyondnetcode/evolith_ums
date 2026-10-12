namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class Name : StringValueObject
{
    private Name(string value) : base(value) { }
    public static Name Create(string value) => new Name(value?.Trim() ?? string.Empty);
    public static Name Default() => new Name(string.Empty);
    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new GenericStringValidator(this, nameof(Name)));
    }
}
