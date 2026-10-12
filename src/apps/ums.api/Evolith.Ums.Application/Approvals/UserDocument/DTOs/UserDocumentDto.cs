namespace Evolith.Ums.Application.Approvals.UserDocument.DTOs;

public sealed record UserDocumentDto(
    Guid UserDocumentId,
    Guid UserId,
    Guid DocumentTypeId,
    DateTime IssueDate,
    DateTime ExpirationDate,
    string Status,
    string Criticity,
    string FileStoragePath,
    string FileChecksum,
    int NotificationStep);
