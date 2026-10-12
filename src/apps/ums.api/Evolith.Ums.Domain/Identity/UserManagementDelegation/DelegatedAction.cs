namespace Evolith.Ums.Domain.Identity.UserManagementDelegation;

public class DelegatedAction : DomainEnumeration
{
    public static readonly DelegatedAction CreateUser     = new(1, nameof(CreateUser));
    public static readonly DelegatedAction BlockUser      = new(2, nameof(BlockUser));
    public static readonly DelegatedAction AssignProfile  = new(3, nameof(AssignProfile));
    public static readonly DelegatedAction ResetPassword  = new(4, nameof(ResetPassword));
    public static readonly DelegatedAction RevokeMfa      = new(5, nameof(RevokeMfa));
    public static readonly DelegatedAction ManageProfilePermissions = new(6, nameof(ManageProfilePermissions));

    private DelegatedAction(int id, string name) : base(id, name) { }
}
