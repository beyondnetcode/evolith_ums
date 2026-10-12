namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class TextValueObject : StringValueObject
{
    private TextValueObject(string value) : base(value) { }

    public static TextValueObject Create(string value)
    {
        return new TextValueObject(value ?? string.Empty);
    }
}
