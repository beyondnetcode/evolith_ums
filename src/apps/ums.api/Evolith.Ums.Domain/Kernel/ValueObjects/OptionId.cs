namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class OptionId : IdValueObject
{
    private OptionId(Guid value) : base(value) { }
    public static new OptionId Create() => new OptionId(Guid.NewGuid());
    public static new OptionId Load(Guid value) => new OptionId(value);
    public static new OptionId Load(string value) => new OptionId(Guid.Parse(value));
}
