namespace Evolith.Ums.Domain.Identity.UserManagementDelegation;

public class DelegationStatus : DomainEnumeration
{
    public static readonly DelegationStatus Draft            = new(1, nameof(Draft));
    public static readonly DelegationStatus PendingApproval  = new(2, nameof(PendingApproval));
    public static readonly DelegationStatus Active           = new(3, nameof(Active));
    public static readonly DelegationStatus Revoked          = new(4, nameof(Revoked));
    public static readonly DelegationStatus Expired          = new(5, nameof(Expired));
    public static readonly DelegationStatus Completed        = new(6, nameof(Completed));
    public static readonly DelegationStatus Rejected         = new(7, nameof(Rejected));
    public static readonly DelegationStatus Archived         = new(8, nameof(Archived));

    private DelegationStatus(int id, string name) : base(id, name) { }
}
