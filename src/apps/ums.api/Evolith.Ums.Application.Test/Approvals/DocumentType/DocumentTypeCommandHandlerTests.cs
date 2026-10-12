namespace Evolith.Ums.Application.Test.Approvals.DocumentType;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Approvals.DocumentType.Commands;
using Evolith.Ums.Domain.Approvals.DocumentType;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Approvals;
using Moq;
using Xunit;
using System;
using System.Threading;
using System.Threading.Tasks;

public class DocumentTypeCommandHandlerTests
{
    private readonly Mock<IDocumentTypeRepository> _repo = new();
    private readonly Mock<IUnitOfWork>             _uow  = new();
    private readonly Mock<IUserContext>            _ctx  = new();

    public DocumentTypeCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
    }

    // =========================================================================
    #region CreateDocumentTypeCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");

        var cmd = new CreateDocumentTypeCommand(
            TenantId: Guid.NewGuid(),
            Code: "DOCTYPE-001",
            Name: "Passport",
            Description: "Government-issued passport",
            Criticity: "High");

        var handler = new CreateDocumentTypeCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.DocumentTypeId);
        _repo.Verify(r => r.AddAsync(It.IsAny<DocumentType>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreateDocumentTypeCommand(
            TenantId: Guid.NewGuid(),
            Code: "DOCTYPE-001",
            Name: "Passport",
            Description: "Government-issued passport",
            Criticity: "High");

        var handler = new CreateDocumentTypeCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region UpdateDocumentTypeCommandHandler
    // =========================================================================

    [Fact]
    public async Task Update_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var entity = MakeDocumentType();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(entity);

        var cmd = new UpdateDocumentTypeCommand(entity.Props.Id.GetValue(), "Updated Name", "Updated Description");
        var handler = new UpdateDocumentTypeCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((DocumentType?)null);

        var cmd = new UpdateDocumentTypeCommand(Guid.NewGuid(), "Updated Name", "Updated Description");
        var handler = new UpdateDocumentTypeCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new UpdateDocumentTypeCommand(Guid.NewGuid(), "Updated Name", "Updated Description");
        var handler = new UpdateDocumentTypeCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    private static DocumentType MakeDocumentType(string code = "DOCTYPE-001")
    {
        return DocumentType.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create(code),
            Name.Create("Passport"),
            Description.Create("Passport Description"),
            DocumentCriticity.High,
            ActorId.Create("user-001")).Value;
    }
}
