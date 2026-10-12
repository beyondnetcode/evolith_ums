namespace Evolith.Ums.Application.Test.Approvals.ApprovalWorkflow;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Approvals.ApprovalWorkflow.Commands;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalWorkflow;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class ApprovalWorkflowCommandHandlerTests
{
    private readonly Mock<IApprovalWorkflowRepository> _repo = new();
    private readonly Mock<IUnitOfWork>                 _uow  = new();
    private readonly Mock<IUserContext>                _ctx  = new();

    public ApprovalWorkflowCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
    }

    private static ApprovalWorkflow MakeWorkflow(string code, string name)
    {
        return ApprovalWorkflow.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create(code),
            Name.Create(name),
            Description.Create("Workflow Description"),
            UserCategory.Internal,
            requiresApproval: true,
            systemSuiteId: SystemSuiteId.Load(Guid.NewGuid()),
            createdBy: ActorId.Create("user-001"),
            requiredDocumentCount: 1).Value;
    }

    // =========================================================================
    #region CreateApprovalWorkflowCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");

        var cmd = new CreateApprovalWorkflowCommand(
            TenantId: Guid.NewGuid(),
            Code: "WORKFLOW-001",
            Name: "Test Workflow",
            Description: "Test Workflow Description",
            TargetUserCategory: "Internal",
            RequiresApproval: true,
            SystemSuiteId: null);

        var handler = new CreateApprovalWorkflowCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.ApprovalWorkflowId);
        _repo.Verify(r => r.AddAsync(It.IsAny<ApprovalWorkflow>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreateApprovalWorkflowCommand(
            TenantId: Guid.NewGuid(),
            Code: "WORKFLOW-001",
            Name: "Test Workflow",
            Description: "Test Workflow Description",
            TargetUserCategory: "Internal",
            RequiresApproval: true,
            SystemSuiteId: null);

        var handler = new CreateApprovalWorkflowCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region AddRequiredDocumentCommandHandler
    // =========================================================================

    [Fact]
    public async Task AddRequiredDocument_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var workflow = MakeWorkflow("WF-001", "Test Workflow");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(workflow);

        var cmd = new AddRequiredDocumentCommand(workflow.Props.Id.GetValue(), Guid.NewGuid(), true);
        var handler = new AddRequiredDocumentCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(workflow, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddRequiredDocument_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((ApprovalWorkflow?)null);

        var cmd = new AddRequiredDocumentCommand(Guid.NewGuid(), Guid.NewGuid(), true);
        var handler = new AddRequiredDocumentCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("workflow not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddRequiredDocument_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new AddRequiredDocumentCommand(Guid.NewGuid(), Guid.NewGuid(), true);
        var handler = new AddRequiredDocumentCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region RemoveRequiredDocumentCommandHandler
    // =========================================================================

    [Fact]
    public async Task RemoveRequiredDocument_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var workflow = MakeWorkflow("WF-002", "Test Workflow 2");
        var firstDocTypeId = Guid.NewGuid();
        var secondDocTypeId = Guid.NewGuid();
        workflow.AddRequiredDocument(DocumentTypeId.Load(firstDocTypeId), true, ActorId.Create("user-001"));
        workflow.AddRequiredDocument(DocumentTypeId.Load(secondDocTypeId), true, ActorId.Create("user-001"));
        var docId = workflow.RequiredDocuments.First().GetId().GetValue();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(workflow);

        var cmd = new RemoveRequiredDocumentCommand(workflow.Props.Id.GetValue(), docId);
        var handler = new RemoveRequiredDocumentCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(workflow, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveRequiredDocument_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((ApprovalWorkflow?)null);

        var cmd = new RemoveRequiredDocumentCommand(Guid.NewGuid(), Guid.NewGuid());
        var handler = new RemoveRequiredDocumentCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("workflow not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoveRequiredDocument_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new RemoveRequiredDocumentCommand(Guid.NewGuid(), Guid.NewGuid());
        var handler = new RemoveRequiredDocumentCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
