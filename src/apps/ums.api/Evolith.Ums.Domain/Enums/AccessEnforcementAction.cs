namespace Evolith.Ums.Domain.Enums;

public class AccessEnforcementAction : DomainEnumeration
{
    public static readonly AccessEnforcementAction BlockUser = new(1, nameof(BlockUser));
    public static readonly AccessEnforcementAction RestrictProfile = new(2, nameof(RestrictProfile));
    public static readonly AccessEnforcementAction LogOnly = new(3, nameof(LogOnly));

    private AccessEnforcementAction(int id, string name) : base(id, name) { }
}
