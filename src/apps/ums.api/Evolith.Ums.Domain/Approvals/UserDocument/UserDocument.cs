namespace Evolith.Ums.Domain.Approvals.UserDocument;

using Evolith.Ums.Domain.Approvals.UserDocument.AccessNotification;
using Evolith.Ums.Domain.Approvals.UserDocument.Events;
using AccessNotificationEntity = Evolith.Ums.Domain.Approvals.UserDocument.AccessNotification.AccessNotification;

public sealed class UserDocument : AggregateRoot<UserDocument, UserDocumentProps>
{
    private readonly List<AccessNotificationEntity> _notifications = new();

    public new UserDocumentDomainEventsManager DomainEvents { get; }

    private UserDocument(UserDocumentProps props) : base(props)
    {
        DomainEvents = new UserDocumentDomainEventsManager(this);

        if (TrackingState.IsNew)
        {
            DomainEvents.RaiseEvent(new DocumentUploadedEvent(
                props.Id.GetValue(), props.UserId.GetValue(),
                props.DocumentTypeId.GetValue(), props.ExpirationDate));
        }
    }

    public UserId UserId => Props.UserId;
    public DocumentTypeId DocumentTypeId => Props.DocumentTypeId;
    public DateTime IssueDate => Props.IssueDate;
    public DateTime ExpirationDate => Props.ExpirationDate;
    public DocumentStatus Status => Props.Status;
    public DocumentCriticity Criticity => Props.Criticity;
    public TextValueObject FileStoragePath => Props.FileStoragePath;
    public string FileChecksum => Props.FileChecksum;
    public int NotificationStep => Props.NotificationStep;

    public IReadOnlyCollection<AccessNotificationEntity> Notifications => _notifications.AsReadOnly();

    public UserDocumentId GetId() => UserDocumentId.Load(Props.Id.GetValue());

    // Upload starts lifecycle at PENDING_REVIEW (INV-UD FSM)
    public static Result<UserDocument> Upload(
        UserId userId,
        DocumentTypeId documentTypeId,
        DateTime issueDate,
        DateTime expirationDate,
        DocumentCriticity criticity,
        TextValueObject fileStoragePath,
        string fileChecksum,
        ActorId createdBy)
    {
        // INV-UD1: ExpirationDate > IssueDate
        if (expirationDate <= issueDate)
        {
            return Result<UserDocument>.Failure(DomainErrors.Compliance.ExpirationBeforeIssueDate);
        }

        var props = new UserDocumentProps(
            IdValueObject.Create(), userId, documentTypeId,
            issueDate, expirationDate, criticity, fileStoragePath, fileChecksum, createdBy);

        var document = new UserDocument(props);

        if (!document.IsValid())
        {
            return Result<UserDocument>.Failure(document.BrokenRules.GetBrokenRulesAsString());
        }

        return Result<UserDocument>.Success(document);
    }

    // Reviewer validates: PENDING_REVIEW → VALID
    public Result Validate(ActorId validatedBy)
    {
        if (Status != DocumentStatus.PendingReview)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Compliance.DocumentNotPendingReview));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = DocumentStatus.Valid;
        DomainEvents.RaiseEvent(new DocumentValidatedEvent(
            Props.Id.GetValue(), Props.UserId.GetValue(), validatedBy.GetValue()));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(validatedBy.GetValue());
        return Result.Success();
    }

    // Reviewer rejects: PENDING_REVIEW → REJECTED
    // INV-UD2: REJECTED cannot go to VALID directly
    public Result Reject(string rejectionReason, ActorId rejectedBy)
    {
        if (Status != DocumentStatus.PendingReview)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Compliance.DocumentNotPendingReview));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = DocumentStatus.Rejected;
        DomainEvents.RaiseEvent(new DocumentRejectedEvent(
            Props.Id.GetValue(), Props.UserId.GetValue(), rejectionReason));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(rejectedBy.GetValue());
        return Result.Success();
    }

    // Background worker expires: VALID → EXPIRED (INV-UD3)
    public Result Expire(ActorId actor)
    {
        // INV-UD3: EXPIRED solo es alcanzable desde VALID. Se rechaza cualquier salto
        // del ciclo de vida (PENDING_REVIEW → EXPIRED o REJECTED → EXPIRED).
        if (Status == DocumentStatus.Expired)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Compliance.DocumentAlreadyExpired));
        }
        else if (Status != DocumentStatus.Valid)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Compliance.DocumentCannotTransition));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.Status = DocumentStatus.Expired;
        DomainEvents.RaiseEvent(new DocumentExpiredEvent(
            Props.Id.GetValue(), Props.UserId.GetValue(),
            Props.DocumentTypeId.GetValue(), Props.Criticity.Name));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actor.GetValue());
        return Result.Success();
    }

    // New upload re-enters lifecycle: EXPIRED/REJECTED → PENDING_REVIEW
    public Result ReUpload(
        DateTime newIssueDate,
        DateTime newExpirationDate,
        TextValueObject newFileStoragePath,
        string newFileChecksum,
        ActorId actor)
    {
        if (Status != DocumentStatus.Expired && Status != DocumentStatus.Rejected)
        {
            BrokenRules.Add(new BrokenRule(nameof(Status), DomainErrors.Compliance.DocumentCannotTransition));
        }

        if (newExpirationDate <= newIssueDate)
        {
            BrokenRules.Add(new BrokenRule(nameof(ExpirationDate), DomainErrors.Compliance.ExpirationBeforeIssueDate));
        }

        if (!IsValid())
        {
            return Result.Failure(BrokenRules.GetBrokenRulesAsString());
        }

        Props.IssueDate = newIssueDate;
        Props.ExpirationDate = newExpirationDate;
        Props.FileStoragePath = newFileStoragePath;
        Props.FileChecksum = newFileChecksum;
        Props.Status = DocumentStatus.PendingReview;
        Props.NotificationStep = 0;
        DomainEvents.RaiseEvent(new DocumentUploadedEvent(
            Props.Id.GetValue(), Props.UserId.GetValue(),
            Props.DocumentTypeId.GetValue(), newExpirationDate));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actor.GetValue());
        return Result.Success();
    }

    // Records a notification sent by the background worker
    public Result RecordNotificationSent(int step, NotificationChannel channel, int daysRemaining, ActorId actor)
    {
        _notifications.Add(AccessNotificationEntity.Record(step, channel, daysRemaining));
        Props.NotificationStep = step;
        DomainEvents.RaiseEvent(new DocumentNearExpirationEvent(
            Props.Id.GetValue(), Props.UserId.GetValue(),
            Props.DocumentTypeId.GetValue(), daysRemaining, step));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actor.GetValue());
        return Result.Success();
    }

    // Records enforcement action executed by the system
    public Result RecordEnforcementExecuted(string action, ActorId actor)
    {
        DomainEvents.RaiseEvent(new EnforcementExecutedEvent(
            Props.Id.GetValue(), Props.UserId.GetValue(), action, DateTime.UtcNow));
        TrackingState.MarkAsDirty();
        Props.Audit.Update(actor.GetValue());
        return Result.Success();
    }
}
