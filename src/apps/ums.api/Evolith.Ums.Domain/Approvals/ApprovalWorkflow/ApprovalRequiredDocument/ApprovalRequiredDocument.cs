namespace Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalRequiredDocument;

public sealed class ApprovalRequiredDocument : Entity<ApprovalRequiredDocument, ApprovalRequiredDocumentProps>
{
    private ApprovalRequiredDocument(ApprovalRequiredDocumentProps props) : base(props)
    {
    }

    public ApprovalWorkflowId WorkflowId => Props.WorkflowId;
    public DocumentTypeId DocumentTypeId => Props.DocumentTypeId;
    public bool IsMandatory => Props.IsMandatory;

    public ApprovalRequiredDocumentId GetId() => ApprovalRequiredDocumentId.Load(Props.Id.GetValue());

    public static Result<ApprovalRequiredDocument> Create(
        ApprovalWorkflowId workflowId,
        DocumentTypeId documentTypeId,
        bool isMandatory,
        ActorId createdBy)
    {
        var props = new ApprovalRequiredDocumentProps(IdValueObject.Create(), workflowId, documentTypeId, isMandatory, createdBy);
        var document = new ApprovalRequiredDocument(props);

        if (!document.IsValid())
        {
            return Result<ApprovalRequiredDocument>.Failure(document.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<ApprovalRequiredDocument>.Success(document);
    }
}
