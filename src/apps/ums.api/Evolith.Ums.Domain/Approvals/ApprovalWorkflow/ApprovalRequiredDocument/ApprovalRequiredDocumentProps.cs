namespace Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalRequiredDocument;

public class ApprovalRequiredDocumentProps : IProps
{
    public IdValueObject Id { get; set; }
    public ApprovalWorkflowId WorkflowId { get; set; }
    public DocumentTypeId DocumentTypeId { get; set; }
    public bool IsMandatory { get; set; }
    public AuditValueObject Audit { get; private set; }

    public ApprovalRequiredDocumentProps(
        IdValueObject id,
        ApprovalWorkflowId workflowId,
        DocumentTypeId documentTypeId,
        bool isMandatory,
        ActorId createdBy)
    {
        Id = id;
        WorkflowId = workflowId;
        DocumentTypeId = documentTypeId;
        IsMandatory = isMandatory;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
