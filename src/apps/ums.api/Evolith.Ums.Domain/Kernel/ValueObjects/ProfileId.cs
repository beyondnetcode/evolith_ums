namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ProfileId : IdValueObject
{
    private ProfileId(Guid value) : base(value) { }
    public static new ProfileId Create() => new ProfileId(Guid.NewGuid());
    public static new ProfileId Load(Guid value) => new ProfileId(value);
    public static new ProfileId Load(string value) => new ProfileId(Guid.Parse(value));
}
