namespace Evolith.Ums.Domain.Enums;

public class OrganizationType : DomainEnumeration
{
    public static readonly OrganizationType INTERNAL = new(1, nameof(INTERNAL));
    public static readonly OrganizationType SUPPLIER = new(2, nameof(SUPPLIER));
    public static readonly OrganizationType CLIENT = new(3, nameof(CLIENT));

    private OrganizationType(int id, string name) : base(id, name) { }
}
