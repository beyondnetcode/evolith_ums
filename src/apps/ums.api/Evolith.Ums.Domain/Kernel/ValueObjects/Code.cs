namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class Code : StringValueObject
{
    private Code(string value) : base(value)
    {
    }

    public static Code Create(string value)
    {
        return new Code(DomainGuards.NormalizeCode(value));
    }

    public static Code Default()
    {
        return new Code(string.Empty);
    }

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new CodeValidator(this));
    }
}
