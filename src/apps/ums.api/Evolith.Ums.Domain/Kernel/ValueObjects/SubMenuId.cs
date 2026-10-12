namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class SubMenuId : IdValueObject
{
    private SubMenuId(Guid value) : base(value) { }
    public static new SubMenuId Create() => new SubMenuId(Guid.NewGuid());
    public static new SubMenuId Load(Guid value) => new SubMenuId(value);
    public static new SubMenuId Load(string value) => new SubMenuId(Guid.Parse(value));
}
