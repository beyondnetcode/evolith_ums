namespace Evolith.Ums.Application.Test.Approvals.UserDocument;

using System;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Approvals.UserDocument.Commands;
using Evolith.Ums.Application.Approvals.UserDocument.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.UserDocument;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Moq;
using Xunit;

public class UserDocumentCommandHandlerTests
{
    private readonly Mock<IUserDocumentRepository> _repo            = new();
    private readonly Mock<IUserAccountRepository>  _userAccountRepo = new();
    private readonly Mock<INotificationService>    _notifications   = new();
    private readonly Mock<IUnitOfWork>             _uow             = new();
    private readonly Mock<IUserContext>            _ctx             = new();

    private static readonly Guid TenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    public UserDocumentCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
    }

    private static UserDocument MakeUserDocument() =>
        UserDocument.Upload(
            UserId.Load(Guid.NewGuid()),
            DocumentTypeId.Load(Guid.NewGuid()),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(10),
            DocumentCriticity.High,
            TextValueObject.Create("docs/my-file.pdf"),
            "MD5-12345",
            ActorId.Create("user-001")).Value;

    private static UserAccount MakeOwner()
    {
        var user = UserAccount.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(TenantId),
            Email.Create("owner@example.com"),
            UserCategory.Internal,
            null, null,
            ActorId.Create("sys")).Value;
        user.Activate(ActorId.Create("sys"));
        return user;
    }

    private UploadUserDocumentCommandHandler CreateUploadHandler() =>
        new(_repo.Object, _ctx.Object);

    private ValidateUserDocumentCommandHandler CreateValidateHandler() =>
        new(_repo.Object, _userAccountRepo.Object, _notifications.Object, _ctx.Object);

    private RejectUserDocumentCommandHandler CreateRejectHandler() =>
        new(_repo.Object, _userAccountRepo.Object, _notifications.Object, _ctx.Object);

    private ReUploadUserDocumentCommandHandler CreateReUploadHandler() =>
        new(_repo.Object, _ctx.Object);

    private ExpireUserDocumentCommandHandler CreateExpireHandler() =>
        new(_repo.Object, _ctx.Object);

    private RecordEnforcementExecutedCommandHandler CreateEnforcementHandler() =>
        new(_repo.Object, _ctx.Object);

    // =========================================================================
    #region UploadUserDocumentCommandHandler
    // =========================================================================

    [Fact]
    public async Task Upload_WithValidCommand_ReturnsSuccess()
    {
        var cmd = new UploadUserDocumentCommand(
            UserId: Guid.NewGuid(),
            DocumentTypeId: Guid.NewGuid(),
            IssueDate: DateTime.UtcNow.AddDays(-1),
            ExpirationDate: DateTime.UtcNow.AddDays(10),
            Criticity: "High",
            FileStoragePath: "docs/my-file.pdf",
            FileChecksum: "MD5-12345");

        var result = await CreateUploadHandler().Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.UserDocumentId);
        _repo.Verify(r => r.AddAsync(It.IsAny<UserDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var result = await CreateUploadHandler().Handle(
            new UploadUserDocumentCommand(Guid.NewGuid(), Guid.NewGuid(),
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10),
                "High", "docs/my-file.pdf", "MD5-12345"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_WhenExpirationBeforeIssueDate_ReturnsFailure()
    {
        var result = await CreateUploadHandler().Handle(
            new UploadUserDocumentCommand(Guid.NewGuid(), Guid.NewGuid(),
                DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(1),
                "High", "docs/my-file.pdf", "MD5-12345"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region ValidateUserDocumentCommandHandler
    // =========================================================================

    [Fact]
    public async Task Validate_WithValidCommand_ReturnsSuccess()
    {
        var doc = MakeUserDocument();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeOwner());

        var result = await CreateValidateHandler().Handle(
            new ValidateUserDocumentCommand(doc.Props.Id.GetValue()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Valid, doc.Status);
        _repo.Verify(r => r.UpdateAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validate_WithValidCommand_SendsNotificationToOwner()
    {
        var doc = MakeUserDocument();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeOwner());

        await CreateValidateHandler().Handle(
            new ValidateUserDocumentCommand(doc.Props.Id.GetValue()), CancellationToken.None);

        _notifications.Verify(
            n => n.SendAsync(
                It.Is<UmsNotification>(n => n.Recipient == "owner@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Validate_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserDocument?)null);

        var result = await CreateValidateHandler().Handle(
            new ValidateUserDocumentCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("user document not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Validate_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var result = await CreateValidateHandler().Handle(
            new ValidateUserDocumentCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Validate_WhenNotPendingReview_ReturnsFailure()
    {
        var doc = MakeUserDocument();
        doc.Validate(ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var result = await CreateValidateHandler().Handle(
            new ValidateUserDocumentCommand(doc.Props.Id.GetValue()), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region RejectUserDocumentCommandHandler
    // =========================================================================

    [Fact]
    public async Task Reject_WithValidCommand_ReturnsSuccess()
    {
        var doc = MakeUserDocument();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeOwner());

        var result = await CreateRejectHandler().Handle(
            new RejectUserDocumentCommand(doc.Props.Id.GetValue(), "Document is blurry"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Rejected, doc.Status);
        _repo.Verify(r => r.UpdateAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_WithValidCommand_SendsNotificationToOwner()
    {
        var doc = MakeUserDocument();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);
        _userAccountRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeOwner());

        await CreateRejectHandler().Handle(
            new RejectUserDocumentCommand(doc.Props.Id.GetValue(), "Document is blurry"), CancellationToken.None);

        _notifications.Verify(
            n => n.SendAsync(
                It.Is<UmsNotification>(n => n.Recipient == "owner@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Reject_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserDocument?)null);

        var result = await CreateRejectHandler().Handle(
            new RejectUserDocumentCommand(Guid.NewGuid(), "blurry"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("user document not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reject_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var result = await CreateRejectHandler().Handle(
            new RejectUserDocumentCommand(Guid.NewGuid(), "blurry"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reject_WhenNotPendingReview_ReturnsFailure()
    {
        var doc = MakeUserDocument();
        doc.Validate(ActorId.Create("user-001")); // → Valid, cannot reject from Valid

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var result = await CreateRejectHandler().Handle(
            new RejectUserDocumentCommand(doc.Props.Id.GetValue(), "Late rejection"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region ReUploadUserDocumentCommandHandler
    // =========================================================================

    [Fact]
    public async Task ReUpload_WithValidCommand_ReturnsSuccess()
    {
        var doc = MakeUserDocument();
        doc.Validate(ActorId.Create("user-001")); // PendingReview → Valid
        doc.Expire(ActorId.Create("user-001"));   // Valid → Expired (INV-UD3)
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var result = await CreateReUploadHandler().Handle(
            new ReUploadUserDocumentCommand(doc.Props.Id.GetValue(),
                DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(20),
                "docs/new-file.pdf", "MD5-67890"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReUpload_WhenDocumentIsValid_ReturnsFailure()
    {
        var doc = MakeUserDocument();
        doc.Validate(ActorId.Create("user-001")); // Valid → cannot re-upload

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var result = await CreateReUploadHandler().Handle(
            new ReUploadUserDocumentCommand(doc.Props.Id.GetValue(),
                DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(20),
                "docs/new-file.pdf", "MD5-67890"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ReUpload_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserDocument?)null);

        var result = await CreateReUploadHandler().Handle(
            new ReUploadUserDocumentCommand(Guid.NewGuid(),
                DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(20),
                "docs/new-file.pdf", "MD5-67890"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("user document not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReUpload_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var result = await CreateReUploadHandler().Handle(
            new ReUploadUserDocumentCommand(Guid.NewGuid(),
                DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(20),
                "docs/new-file.pdf", "MD5-67890"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReUpload_WhenExpirationBeforeIssueDate_ReturnsFailure()
    {
        var doc = MakeUserDocument();
        doc.Validate(ActorId.Create("user-001")); // PendingReview → Valid
        doc.Expire(ActorId.Create("user-001"));   // Valid → Expired (INV-UD3)
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var result = await CreateReUploadHandler().Handle(
            new ReUploadUserDocumentCommand(doc.Props.Id.GetValue(),
                DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(1),
                "docs/new-file.pdf", "MD5-67890"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region ExpireUserDocumentCommandHandler
    // =========================================================================

    [Fact]
    public async Task Expire_WithValidCommand_ReturnsSuccess()
    {
        var doc = MakeUserDocument();
        doc.Validate(ActorId.Create("user-001")); // PendingReview → Valid (Expire requires Valid, INV-UD3)
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var result = await CreateExpireHandler().Handle(
            new ExpireUserDocumentCommand(doc.Props.Id.GetValue()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Expired, doc.Status);
        _repo.Verify(r => r.UpdateAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Expire_WhenAlreadyExpired_ReturnsFailure()
    {
        var doc = MakeUserDocument();
        doc.Validate(ActorId.Create("sys")); // PendingReview → Valid
        doc.Expire(ActorId.Create("sys"));   // Valid → Expired
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var result = await CreateExpireHandler().Handle(
            new ExpireUserDocumentCommand(doc.Props.Id.GetValue()), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Expire_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserDocument?)null);

        var result = await CreateExpireHandler().Handle(
            new ExpireUserDocumentCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("user document not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expire_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var result = await CreateExpireHandler().Handle(
            new ExpireUserDocumentCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region RecordEnforcementExecutedCommandHandler
    // =========================================================================

    [Fact]
    public async Task RecordEnforcement_WithValidCommand_ReturnsSuccess()
    {
        var doc = MakeUserDocument();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(doc);

        var result = await CreateEnforcementHandler().Handle(
            new RecordEnforcementExecutedCommand(doc.Props.Id.GetValue(), "ACCESS_REVOKED"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(doc, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordEnforcement_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserDocument?)null);

        var result = await CreateEnforcementHandler().Handle(
            new RecordEnforcementExecutedCommand(Guid.NewGuid(), "ACCESS_REVOKED"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("user document not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecordEnforcement_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var result = await CreateEnforcementHandler().Handle(
            new RecordEnforcementExecutedCommand(Guid.NewGuid(), "ACCESS_REVOKED"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
