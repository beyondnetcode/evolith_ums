namespace Evolith.Ums.Application.Test.Approvals.ApprovalWorkflow;

using Evolith.Ums.Application.Approvals.ApprovalWorkflow.Queries;
using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;
using Evolith.Ums.Domain.Approvals.ApprovalWorkflow;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class ApprovalWorkflowQueryHandlerTests
{
    private readonly Mock<IApprovalWorkflowRepository> _repo = new();

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
    #region GetApprovalWorkflowByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var workflow = MakeWorkflow("WF-001", "Workflow A");
        var workflowId = workflow.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(workflow);

        var query = new GetApprovalWorkflowByIdQuery(workflowId);
        var handler = new GetApprovalWorkflowByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(workflowId, result.Value.ApprovalWorkflowId);
        Assert.Equal("WF-001", result.Value.Code);
        Assert.Equal("Workflow A", result.Value.Name);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((ApprovalWorkflow?)null);

        var query = new GetApprovalWorkflowByIdQuery(Guid.NewGuid());
        var handler = new GetApprovalWorkflowByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("workflow not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllApprovalWorkflowsQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_WithoutTenantFilter_ReturnsAllItems()
    {
        var wf1 = MakeWorkflow("WF-001", "Workflow A");
        var wf2 = MakeWorkflow("WF-002", "Workflow B");
        var list = new List<ApprovalWorkflow> { wf1, wf2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalWorkflowsQuery(
            TenantId: null,
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllApprovalWorkflowsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalItems);
        Assert.Equal(1, result.Value.Page);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetAll_WithTenantFilter_ReturnsTenantItems()
    {
        var tenantId = Guid.NewGuid();
        var wf1 = MakeWorkflow("WF-001", "Workflow A");
        var list = new List<ApprovalWorkflow> { wf1 };

        _repo.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalWorkflowsQuery(
            TenantId: tenantId,
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        // Admin context: admins may filter by any tenantId via request.TenantId
        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        var handler = new GetAllApprovalWorkflowsQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAll_WithSearch_FiltersResults()
    {
        var wf1 = MakeWorkflow("WF-001", "Workflow Alpha");
        var wf2 = MakeWorkflow("WF-002", "Workflow Beta");
        var list = new List<ApprovalWorkflow> { wf1, wf2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalWorkflowsQuery(
            TenantId: null,
            Search: "beta",
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllApprovalWorkflowsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal("WF-002", result.Value.Items[0].Code);
    }

    [Fact]
    public async Task GetAll_WithSortingCodeDesc_SortsCorrectly()
    {
        var wf1 = MakeWorkflow("WF-001", "Workflow A");
        var wf2 = MakeWorkflow("WF-002", "Workflow B");
        var list = new List<ApprovalWorkflow> { wf1, wf2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalWorkflowsQuery(
            TenantId: null,
            Search: null,
            SortBy: "code",
            SortOrder: "desc",
            Page: 1,
            PageSize: 10);

        var handler = new GetAllApprovalWorkflowsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("WF-002", result.Value.Items[0].Code);
        Assert.Equal("WF-001", result.Value.Items[1].Code);
    }

    #endregion
}
