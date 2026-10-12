namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class ApprovalRequiredDocumentId : IdValueObject
{
    private ApprovalRequiredDocumentId(Guid value) : base(value) { }
    public static new ApprovalRequiredDocumentId Create() => new ApprovalRequiredDocumentId(Guid.NewGuid());
    public static new ApprovalRequiredDocumentId Load(Guid value) => new ApprovalRequiredDocumentId(value);
    public static new ApprovalRequiredDocumentId Load(string value) => new ApprovalRequiredDocumentId(Guid.Parse(value));
}
