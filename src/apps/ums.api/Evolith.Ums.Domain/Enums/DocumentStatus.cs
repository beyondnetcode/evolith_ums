namespace Evolith.Ums.Domain.Enums;

public class DocumentStatus : DomainEnumeration
{
    public static readonly DocumentStatus PendingReview = new(1, "PENDING_REVIEW");
    public static readonly DocumentStatus Valid         = new(2, nameof(Valid));
    public static readonly DocumentStatus Expired       = new(3, nameof(Expired));
    public static readonly DocumentStatus Rejected      = new(4, nameof(Rejected));

    private DocumentStatus(int id, string name) : base(id, name) { }
}
