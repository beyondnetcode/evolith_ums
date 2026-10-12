namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ProfilePermissionId : IdValueObject
{
    private ProfilePermissionId(Guid value) : base(value) { }
    public static new ProfilePermissionId Create() => new ProfilePermissionId(Guid.NewGuid());
    public static new ProfilePermissionId Load(Guid value) => new ProfilePermissionId(value);
    public static new ProfilePermissionId Load(string value) => new ProfilePermissionId(Guid.Parse(value));
}
