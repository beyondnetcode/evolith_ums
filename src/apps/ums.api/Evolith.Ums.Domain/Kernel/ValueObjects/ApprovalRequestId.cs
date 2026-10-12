namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ApprovalRequestId : IdValueObject
{
    private ApprovalRequestId(Guid value) : base(value) { }
    public static new ApprovalRequestId Create() => new ApprovalRequestId(Guid.NewGuid());
    public static new ApprovalRequestId Load(Guid value) => new ApprovalRequestId(value);
    public static new ApprovalRequestId Load(string value) => new ApprovalRequestId(Guid.Parse(value));
}
