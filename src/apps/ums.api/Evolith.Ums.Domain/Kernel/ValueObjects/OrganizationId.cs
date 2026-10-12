namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class OrganizationId : IdValueObject
{
    private OrganizationId(Guid value) : base(value) { }
    public static new OrganizationId Create() => new OrganizationId(Guid.NewGuid());
    public static new OrganizationId Load(Guid value) => new OrganizationId(value);
    public static new OrganizationId Load(string value) => new OrganizationId(Guid.Parse(value));
}
