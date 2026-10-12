namespace Evolith.Ums.Domain.Enums;

public class LinkedResourceType : DomainEnumeration
{
    public static readonly LinkedResourceType Menu     = new(1, nameof(Menu));
    public static readonly LinkedResourceType Module   = new(2, nameof(Module));
    public static readonly LinkedResourceType Endpoint = new(3, nameof(Endpoint));
    public static readonly LinkedResourceType Workflow = new(4, nameof(Workflow));

    private LinkedResourceType(int id, string name) : base(id, name) { }
}
