namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class Justification : StringValueObject
{
    private Justification(string value) : base(value) { }
    public static Justification Create(string value) => new Justification(value?.Trim() ?? string.Empty);
    public static Justification Default() => new Justification(string.Empty);

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new GenericStringValidator(this, nameof(Justification)));
    }
}
