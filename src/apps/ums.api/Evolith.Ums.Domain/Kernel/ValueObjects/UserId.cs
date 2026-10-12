namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class UserId : IdValueObject
{
    private UserId(Guid value) : base(value) { }
    public static new UserId Create() => new UserId(Guid.NewGuid());
    public static new UserId Load(Guid value) => new UserId(value);
    public static new UserId Load(string value) => new UserId(Guid.Parse(value));
}
