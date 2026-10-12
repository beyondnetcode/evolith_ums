using Evolith.Ums.Application.Approvals.UserDocument.DTOs;

namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;

public sealed record UploadUserDocumentCommand(
    Guid UserId, Guid DocumentTypeId, DateTime IssueDate, DateTime ExpirationDate,
    string Criticity, string FileStoragePath, string FileChecksum) : ICommand<UploadUserDocumentResponse>;
