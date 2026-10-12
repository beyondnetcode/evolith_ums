namespace Evolith.Ums.Domain.Approvals.UserDocument;

public class UserDocumentProps : IProps
{
    public IdValueObject Id { get; set; }
    public UserId UserId { get; set; }
    public DocumentTypeId DocumentTypeId { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DocumentStatus Status { get; set; }
    public DocumentCriticity Criticity { get; set; }
    public TextValueObject FileStoragePath { get; set; }
    public string FileChecksum { get; set; }
    public int NotificationStep { get; set; }
    public AuditValueObject Audit { get; private set; }

    public UserDocumentProps(
        IdValueObject id,
        UserId userId,
        DocumentTypeId documentTypeId,
        DateTime issueDate,
        DateTime expirationDate,
        DocumentCriticity criticity,
        TextValueObject fileStoragePath,
        string fileChecksum,
        ActorId createdBy)
    {
        Id = id;
        UserId = userId;
        DocumentTypeId = documentTypeId;
        IssueDate = issueDate;
        ExpirationDate = expirationDate;
        Status = DocumentStatus.PendingReview;
        Criticity = criticity;
        FileStoragePath = fileStoragePath;
        FileChecksum = fileChecksum;
        NotificationStep = 0;
        Audit = AuditValueObject.Create(createdBy.GetValue());
    }

    public object Clone() => MemberwiseClone();
}
