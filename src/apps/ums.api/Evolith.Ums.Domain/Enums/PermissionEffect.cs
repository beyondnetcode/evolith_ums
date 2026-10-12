namespace Evolith.Ums.Domain.Enums;

public class PermissionEffect : DomainEnumeration
{
    public static readonly PermissionEffect Allow = new(1, nameof(Allow));
    public static readonly PermissionEffect Deny = new(2, nameof(Deny));

    private PermissionEffect(int id, string name) : base(id, name) { }
}
