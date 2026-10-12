namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class SystemId : IdValueObject
{
    private SystemId(Guid value) : base(value) { }
    public static new SystemId Create() => new SystemId(Guid.NewGuid());
    public static new SystemId Load(Guid value) => new SystemId(value);
    public static new SystemId Load(string value) => new SystemId(Guid.Parse(value));
}
