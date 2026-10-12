namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class PermissionTemplateItemId : IdValueObject
{
    private PermissionTemplateItemId(Guid value) : base(value) { }
    public static new PermissionTemplateItemId Create() => new PermissionTemplateItemId(Guid.NewGuid());
    public static new PermissionTemplateItemId Load(Guid value) => new PermissionTemplateItemId(value);
    public static new PermissionTemplateItemId Load(string value) => new PermissionTemplateItemId(Guid.Parse(value));
}
