namespace Evolith.Ums.Domain.Identity.Tenant.TenantParameter;

public class TenantParameterId : IdValueObject
{
    private TenantParameterId(Guid value) : base(value) { }

    public static new TenantParameterId Create() => new(Guid.NewGuid());
    public static new TenantParameterId Load(Guid value) => new(value);
    public static new TenantParameterId Load(string value) => new(Guid.Parse(value));
}