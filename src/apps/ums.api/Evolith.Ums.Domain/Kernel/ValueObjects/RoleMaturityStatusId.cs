namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class RoleMaturityStatusId : IdValueObject
{
    private RoleMaturityStatusId(Guid value) : base(value) { }
    public static new RoleMaturityStatusId Create() => new RoleMaturityStatusId(Guid.NewGuid());
    public static new RoleMaturityStatusId Load(Guid value) => new RoleMaturityStatusId(value);
    public static new RoleMaturityStatusId Load(string value) => new RoleMaturityStatusId(Guid.Parse(value));
}
