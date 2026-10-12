namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class MenuId : IdValueObject
{
    private MenuId(Guid value) : base(value) { }
    public static new MenuId Create() => new MenuId(Guid.NewGuid());
    public static new MenuId Load(Guid value) => new MenuId(value);
    public static new MenuId Load(string value) => new MenuId(Guid.Parse(value));
}
