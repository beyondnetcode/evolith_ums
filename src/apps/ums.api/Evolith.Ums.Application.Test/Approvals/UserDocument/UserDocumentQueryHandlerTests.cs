namespace Evolith.Ums.Application.Test.Approvals.UserDocument;

using Evolith.Ums.Application.Approvals.UserDocument.Queries;
using Evolith.Ums.Application.Approvals.UserDocument.DTOs;
using Evolith.Ums.Domain.Approvals.UserDocument;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class UserDocumentQueryHandlerTests
{
    private readonly Mock<IUserDocumentRepository> _repo = new();

    private static UserDocument MakeUserDocument(DocumentStatus status)
    {
        var doc = UserDocument.Upload(
            UserId.Load(Guid.NewGuid()),
            DocumentTypeId.Load(Guid.NewGuid()),
            DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow.AddDays(10),
            DocumentCriticity.High,
            TextValueObject.Create("docs/my-file.pdf"),
            "MD5-12345",
            ActorId.Create("user-001")).Value;

        if (status == DocumentStatus.Valid)
        {
            doc.Validate(ActorId.Create("user-001"));
        }
        else if (status == DocumentStatus.Rejected)
        {
            doc.Reject("Bad file", ActorId.Create("user-001"));
        }
        else if (status == DocumentStatus.Expired)
        {
            doc.Validate(ActorId.Create("user-001"));
            doc.Expire(ActorId.Create("user-001"));
        }

        return doc;
    }

    // =========================================================================
    #region GetUserDocumentByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var doc = MakeUserDocument(DocumentStatus.PendingReview);
        var docId = doc.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(doc);

        var query = new GetUserDocumentByIdQuery(docId);
        var handler = new GetUserDocumentByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(docId, result.Value.UserDocumentId);
        Assert.Equal("PENDING_REVIEW", result.Value.Status);
        Assert.Equal("docs/my-file.pdf", result.Value.FileStoragePath);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserDocument?)null);

        var query = new GetUserDocumentByIdQuery(Guid.NewGuid());
        var handler = new GetUserDocumentByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("user document not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllUserDocumentsQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_WithoutUserIdFilter_ReturnsAllItems()
    {
        var d1 = MakeUserDocument(DocumentStatus.PendingReview);
        var d2 = MakeUserDocument(DocumentStatus.Valid);
        var list = new List<UserDocument> { d1, d2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllUserDocumentsQuery(
            UserId: null,
            Status: "all",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllUserDocumentsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalItems);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetAll_WithUserIdFilter_ReturnsUserItems()
    {
        // Handler now uses GetAllAsync + in-memory UserId filter (no longer GetByUserIdAsync).
        // Use the document's own UserId as the query filter to ensure match.
        var d1 = MakeUserDocument(DocumentStatus.PendingReview);
        var userId = d1.Props.UserId.GetValue();
        var other = MakeUserDocument(DocumentStatus.PendingReview); // different user
        var list = new List<UserDocument> { d1, other };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((IReadOnlyList<UserDocument>)list);

        var query = new GetAllUserDocumentsQuery(
            UserId: userId,
            Status: "all",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllUserDocumentsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WithStatusFilter_FiltersStatus()
    {
        var d1 = MakeUserDocument(DocumentStatus.PendingReview);
        var d2 = MakeUserDocument(DocumentStatus.Valid);
        var list = new List<UserDocument> { d1, d2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllUserDocumentsQuery(
            UserId: null,
            Status: "Valid",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllUserDocumentsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal("Valid", result.Value.Items[0].Status);
    }

    #endregion
}
