namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Commands;
public sealed record AddRequiredDocumentCommand(Guid WorkflowId, Guid DocumentTypeId, bool IsMandatory) : ICommand;
