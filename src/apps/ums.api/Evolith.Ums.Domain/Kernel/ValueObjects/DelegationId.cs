namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class DelegationId : IdValueObject
{
    private DelegationId(Guid value) : base(value) { }
    public static new DelegationId Create() => new(Guid.NewGuid());
    public static new DelegationId Load(Guid value) => new(value);
    public static new DelegationId Load(string value) => new(Guid.Parse(value));
}
