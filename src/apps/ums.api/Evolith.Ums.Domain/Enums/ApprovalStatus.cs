namespace Evolith.Ums.Domain.Enums;

public class ApprovalStatus : DomainEnumeration
{
    public static readonly ApprovalStatus Pending = new(1, nameof(Pending));
    public static readonly ApprovalStatus Approved = new(2, nameof(Approved));
    public static readonly ApprovalStatus Rejected = new(3, nameof(Rejected));

    private ApprovalStatus(int id, string name) : base(id, name) { }
}
