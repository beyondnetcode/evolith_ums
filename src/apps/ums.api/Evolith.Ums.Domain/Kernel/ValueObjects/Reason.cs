namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class Reason : StringValueObject
{
    private Reason(string value) : base(value) { }
    public static Reason Create(string value) => new Reason(value?.Trim() ?? string.Empty);
    public static Reason Default() => new Reason(string.Empty);
    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new GenericStringValidator(this, nameof(Reason)));
    }
}
