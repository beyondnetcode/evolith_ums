namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class UserAccountId : IdValueObject
{
    private UserAccountId(Guid value) : base(value) { }
    public static new UserAccountId Create() => new UserAccountId(Guid.NewGuid());
    public static new UserAccountId Load(Guid value) => new UserAccountId(value);
    public static new UserAccountId Load(string value) => new UserAccountId(Guid.Parse(value));
}
