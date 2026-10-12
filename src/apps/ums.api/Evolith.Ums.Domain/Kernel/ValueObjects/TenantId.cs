namespace Evolith.Ums.Domain.Kernel.ValueObjects;

using BeyondNetCode.Shell.Ddd;

public class TenantId : IdValueObject
{
    private TenantId(Guid value) : base(value) { }
    public static new TenantId Create() => new TenantId(Guid.NewGuid());
    public static new TenantId Load(Guid value) => new TenantId(value);
    public static new TenantId Load(string value) => new TenantId(Guid.Parse(value));
}
