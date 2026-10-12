namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;
public sealed record ReUploadUserDocumentCommand(
    Guid UserDocumentId,
    DateTime NewIssueDate,
    DateTime NewExpirationDate,
    string NewFileStoragePath,
    string NewFileChecksum) : ICommand;
