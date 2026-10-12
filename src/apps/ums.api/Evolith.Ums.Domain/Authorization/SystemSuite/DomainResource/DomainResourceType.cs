namespace Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;

public class DomainResourceType : DomainEnumeration
{
    public static readonly DomainResourceType Aggregate    = new(1, nameof(Aggregate));
    public static readonly DomainResourceType Entity       = new(2, nameof(Entity));
    public static readonly DomainResourceType DomainMethod = new(3, nameof(DomainMethod));

    private DomainResourceType(int id, string name) : base(id, name) { }
}
