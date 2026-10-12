namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ActionId : IdValueObject
{
    private ActionId(Guid value) : base(value) { }
    public static new ActionId Create() => new ActionId(Guid.NewGuid());
    public static new ActionId Load(Guid value) => new ActionId(value);
    public static new ActionId Load(string value) => new ActionId(Guid.Parse(value));
}
