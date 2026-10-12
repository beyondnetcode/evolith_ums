namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;
public sealed record RejectUserDocumentCommand(Guid UserDocumentId, string RejectionReason) : ICommand;
