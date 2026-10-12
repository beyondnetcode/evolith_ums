namespace Evolith.Ums.Domain.Identity.UserManagementDelegation;

public class DelegationScopeType : DomainEnumeration
{
    public static readonly DelegationScopeType Tenant     = new(1, nameof(Tenant));
    public static readonly DelegationScopeType Organization = new(2, nameof(Organization));
    public static readonly DelegationScopeType Department = new(3, nameof(Department));
    public static readonly DelegationScopeType System     = new(4, nameof(System));
    public static readonly DelegationScopeType Team       = new(5, nameof(Team));

    private DelegationScopeType(int id, string name) : base(id, name) { }
}
