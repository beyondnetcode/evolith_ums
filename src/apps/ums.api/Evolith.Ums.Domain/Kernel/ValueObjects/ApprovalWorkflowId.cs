namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ApprovalWorkflowId : IdValueObject
{
    private ApprovalWorkflowId(Guid value) : base(value) { }
    public static new ApprovalWorkflowId Create() => new ApprovalWorkflowId(Guid.NewGuid());
    public static new ApprovalWorkflowId Load(Guid value) => new ApprovalWorkflowId(value);
    public static new ApprovalWorkflowId Load(string value) => new ApprovalWorkflowId(Guid.Parse(value));
}
