namespace Evolith.Ums.Domain.Test.Approvals.UserDocument;

using Evolith.Ums.Domain.Approvals.UserDocument;
using Xunit;

public class UserDocumentTests
{
    private static readonly UserId ValidUserId = UserId.Load(Guid.NewGuid().ToString());
    private static readonly DocumentTypeId ValidDocumentTypeId = DocumentTypeId.Load(Guid.NewGuid().ToString());
    private static readonly DateTime ValidIssueDate = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ValidExpirationDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DocumentCriticity ValidCriticity = DocumentCriticity.High;
    private static readonly TextValueObject ValidFileStoragePath = TextValueObject.Create("/storage/doc.pdf");
    private static readonly string ValidFileChecksum = "abc123def456";
    private static readonly ActorId ValidActor = ActorId.Create("user-001");
    #region Upload

    [Fact]
    public void Upload_WithValidData_ReturnsSuccess()
    {
        var result = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidUserId, result.Value.UserId);
        Assert.Equal(ValidDocumentTypeId, result.Value.DocumentTypeId);
        Assert.Equal(ValidIssueDate, result.Value.IssueDate);
        Assert.Equal(ValidExpirationDate, result.Value.ExpirationDate);
        Assert.Equal(ValidCriticity, result.Value.Criticity);
        Assert.Equal(DocumentStatus.PendingReview, result.Value.Status);
        Assert.Equal(0, result.Value.NotificationStep);
        Assert.Empty(result.Value.Notifications);
    }

    [Fact]
    public void Upload_WhenExpirationBeforeIssueDate_ReturnsFailure()
    {
        var issueDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expirationDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, issueDate, expirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Compliance.ExpirationBeforeIssueDate, result.Error);
    }

    [Fact]
    public void Upload_WhenExpirationEqualsIssueDate_ReturnsFailure()
    {
        var date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, date, date,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Compliance.ExpirationBeforeIssueDate, result.Error);
    }

    [Fact]
    public void Upload_RaisesDocumentUploadedEvent()
    {
        var result = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor);

        Assert.True(result.IsSuccess);
        var events = result.Value.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Single(events);
        Assert.IsType<DocumentUploadedEvent>(events[0]);
    }

    #endregion

    #region Validate

    [Fact]
    public void Validate_WhenPendingReview_ReturnsSuccess()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;

        var result = document.Validate(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Valid, document.Status);
    }

    [Fact]
    public void Validate_WhenNotPendingReview_ReturnsFailure()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);

        var result = document.Validate(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Compliance.DocumentNotPendingReview, result.Error);
    }

    [Fact]
    public void Validate_RaisesDocumentValidatedEvent()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;

        document.Validate(ValidActor);

        var events = document.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DocumentValidatedEvent);
    }

    #endregion

    #region Reject

    [Fact]
    public void Reject_WhenPendingReview_ReturnsSuccess()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        var reason = "Invalid document format";

        var result = document.Reject(reason, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Rejected, document.Status);
    }

    [Fact]
    public void Reject_WhenNotPendingReview_ReturnsFailure()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);

        var result = document.Reject("Bad format", ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Compliance.DocumentNotPendingReview, result.Error);
    }

    [Fact]
    public void Reject_RaisesDocumentRejectedEvent()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;

        document.Reject("Invalid format", ValidActor);

        var events = document.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DocumentRejectedEvent);
    }

    #endregion

    #region Expire

    [Fact]
    public void Expire_WhenNotExpired_ReturnsSuccess()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);

        var result = document.Expire(ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Expired, document.Status);
    }

    [Fact]
    public void Expire_WhenAlreadyExpired_ReturnsFailure()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);
        document.Expire(ValidActor);

        var result = document.Expire(ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Compliance.DocumentAlreadyExpired, result.Error);
    }

    [Fact]
    public void Expire_RaisesDocumentExpiredEvent()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);

        document.Expire(ValidActor);

        var events = document.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DocumentExpiredEvent);
    }

    #endregion

    #region ReUpload

    [Fact]
    public void ReUpload_WhenExpired_ReturnsSuccess()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);
        document.Expire(ValidActor);
        var newIssueDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newExpirationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newStoragePath = TextValueObject.Create("/storage/new-doc.pdf");
        var newChecksum = "newchecksum789";

        var result = document.ReUpload(newIssueDate, newExpirationDate, newStoragePath, newChecksum, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.PendingReview, document.Status);
        Assert.Equal(0, document.NotificationStep);
    }

    [Fact]
    public void ReUpload_WhenRejected_ReturnsSuccess()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Reject("Invalid", ValidActor);
        var newIssueDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newExpirationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newStoragePath = TextValueObject.Create("/storage/new-doc.pdf");
        var newChecksum = "newchecksum789";

        var result = document.ReUpload(newIssueDate, newExpirationDate, newStoragePath, newChecksum, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.PendingReview, document.Status);
    }

    [Fact]
    public void ReUpload_WhenValid_ReturnsFailure()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);
        var newIssueDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newExpirationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newStoragePath = TextValueObject.Create("/storage/new-doc.pdf");
        var newChecksum = "newchecksum789";

        var result = document.ReUpload(newIssueDate, newExpirationDate, newStoragePath, newChecksum, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Compliance.DocumentCannotTransition, result.Error);
    }

    [Fact]
    public void ReUpload_WithInvalidDates_ReturnsFailure()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);
        document.Expire(ValidActor);
        var newIssueDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newExpirationDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newStoragePath = TextValueObject.Create("/storage/new-doc.pdf");
        var newChecksum = "newchecksum789";

        var result = document.ReUpload(newIssueDate, newExpirationDate, newStoragePath, newChecksum, ValidActor);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Compliance.ExpirationBeforeIssueDate, result.Error);
    }

    [Fact]
    public void ReUpload_RaisesDocumentUploadedEvent()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;
        document.Validate(ValidActor);
        document.Expire(ValidActor);
        var newIssueDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newExpirationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newStoragePath = TextValueObject.Create("/storage/new-doc.pdf");
        var newChecksum = "newchecksum789";

        document.ReUpload(newIssueDate, newExpirationDate, newStoragePath, newChecksum, ValidActor);

        var events = document.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DocumentUploadedEvent);
    }

    #endregion

    #region RecordNotificationSent

    [Fact]
    public void RecordNotificationSent_AddsNotification()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;

        var result = document.RecordNotificationSent(1, NotificationChannel.Email, 30, ValidActor);

        Assert.True(result.IsSuccess);
        Assert.Single(document.Notifications);
        Assert.Equal(1, document.NotificationStep);
    }

    [Fact]
    public void RecordNotificationSent_RaisesDocumentNearExpirationEvent()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;

        document.RecordNotificationSent(1, NotificationChannel.Email, 30, ValidActor);

        var events = document.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is DocumentNearExpirationEvent);
    }

    #endregion

    #region RecordEnforcementExecuted

    [Fact]
    public void RecordEnforcementExecuted_RaisesEnforcementExecutedEvent()
    {
        var document = UserDocument.Upload(
            ValidUserId, ValidDocumentTypeId, ValidIssueDate, ValidExpirationDate,
            ValidCriticity, ValidFileStoragePath, ValidFileChecksum, ValidActor).Value;

        document.RecordEnforcementExecuted("AccessRestricted", ValidActor);

        var events = document.DomainEvents.GetUncommittedChanges().ToList();
        Assert.Contains(events, e => e is EnforcementExecutedEvent);
    }

    #endregion
}
