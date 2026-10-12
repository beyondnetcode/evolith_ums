namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class RoleId : IdValueObject
{
    private RoleId(Guid value) : base(value) { }
    public static new RoleId Create() => new RoleId(Guid.NewGuid());
    public static new RoleId Load(Guid value) => new RoleId(value);
    public static new RoleId Load(string value) => new RoleId(Guid.Parse(value));
}
