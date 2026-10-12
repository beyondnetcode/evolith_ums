namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Commands;
public sealed record RemoveRequiredDocumentCommand(Guid WorkflowId, Guid DocumentId) : ICommand;
