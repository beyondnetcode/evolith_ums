namespace Evolith.Ums.Domain.Enums;

public class UserStatus : DomainEnumeration
{
    public static readonly UserStatus Pending  = new(1, nameof(Pending));
    public static readonly UserStatus Active   = new(2, nameof(Active));
    public static readonly UserStatus Blocked  = new(3, nameof(Blocked));
    /// <summary>REC-16: Soft-deleted — user data is anonymized, account is permanently deactivated.</summary>
    public static readonly UserStatus Deleted  = new(4, nameof(Deleted));
    /// <summary>EP-09: Tenant admin explicitly denied this signup request. Terminal state — user cannot authenticate.</summary>
    public static readonly UserStatus Denied   = new(5, nameof(Denied));

    private UserStatus(int id, string name) : base(id, name) { }
}
