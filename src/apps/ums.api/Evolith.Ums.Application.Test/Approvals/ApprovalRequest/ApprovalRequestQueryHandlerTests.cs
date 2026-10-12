namespace Evolith.Ums.Application.Test.Approvals.ApprovalRequest;

using Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;
using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class ApprovalRequestQueryHandlerTests
{
    private readonly Mock<IApprovalRequestRepository> _repo = new();

    private static readonly SystemSuiteId ValidSystemId = SystemSuiteId.Load(Guid.NewGuid());
    private static readonly RoleId ValidRoleId          = RoleId.Load(Guid.NewGuid());

    private static ApprovalRequest MakeApprovalRequest(ApprovalStatus status)
    {
        var req = ApprovalRequest.Create(
            ApprovalWorkflowId.Load(Guid.NewGuid()),
            UserId.Load(Guid.NewGuid()),
            ProfileId.Load(Guid.NewGuid()),
            ValidSystemId, null, ValidRoleId, null,
            ActorId.Create("user-001")).Value;

        if (status == ApprovalStatus.Approved)
            req.Approve(ActorId.Create("user-001"), ValidRoleId);
        else if (status == ApprovalStatus.Rejected)
            req.Reject(ActorId.Create("user-001"));

        return req;
    }

    private static ApprovalRequest MakeApprovalRequestForUser(Guid targetUserId)
        => ApprovalRequest.Create(
            ApprovalWorkflowId.Load(Guid.NewGuid()),
            UserId.Load(targetUserId),
            ProfileId.Load(Guid.NewGuid()),
            ValidSystemId, null, ValidRoleId, null,
            ActorId.Create("user-001")).Value;

    // =========================================================================
    #region GetApprovalRequestByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var req = MakeApprovalRequest(ApprovalStatus.Pending);
        var reqId = req.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(reqId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(req);

        var query = new GetApprovalRequestByIdQuery(reqId);
        var handler = new GetApprovalRequestByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(reqId, result.Value.ApprovalRequestId);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Null(result.Value.DecisionBy);
        Assert.Null(result.Value.DecisionAt);
    }

    [Fact]
    public async Task GetById_WhenRejected_ReturnsDeniedStatus()
    {
        var req = MakeApprovalRequest(ApprovalStatus.Rejected);
        var reqId = req.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(reqId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(req);

        var query = new GetApprovalRequestByIdQuery(reqId);
        var handler = new GetApprovalRequestByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Denied", result.Value.Status);
        Assert.NotNull(result.Value.DecisionBy);
        Assert.NotNull(result.Value.DecisionAt);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((ApprovalRequest?)null);

        var query = new GetApprovalRequestByIdQuery(Guid.NewGuid());
        var handler = new GetApprovalRequestByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("approval request not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllApprovalRequestsQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_WithoutTenantFilter_ReturnsAllItems()
    {
        var r1 = MakeApprovalRequest(ApprovalStatus.Pending);
        var r2 = MakeApprovalRequest(ApprovalStatus.Approved);
        var list = new List<ApprovalRequest> { r1, r2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalRequestsQuery(
            TenantId: null,
            Status: "all",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllApprovalRequestsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalItems);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetAll_WithTenantFilter_ReturnsTenantItems()
    {
        var tenantId = Guid.NewGuid();
        var r1 = MakeApprovalRequest(ApprovalStatus.Pending);
        var list = new List<ApprovalRequest> { r1 };

        _repo.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalRequestsQuery(
            TenantId: tenantId,
            Status: "all",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        var handler = new GetAllApprovalRequestsQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WithUserIdFilter_ReturnsOnlyTargetUserRequests()
    {
        // G-159: el filtro userId (usuario objetivo) debe aplicarse; antes se ignoraba.
        var targetUser = Guid.NewGuid();
        var mine = MakeApprovalRequestForUser(targetUser);
        var otherA = MakeApprovalRequestForUser(Guid.NewGuid());
        var otherB = MakeApprovalRequestForUser(Guid.NewGuid());
        var list = new List<ApprovalRequest> { otherA, mine, otherB };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalRequestsQuery(
            TenantId: null,
            UserId: targetUser,
            Status: "all",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllApprovalRequestsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal(targetUser, result.Value.Items[0].TargetUserId);
    }

    [Fact]
    public async Task GetAll_WithStatusFilter_FiltersStatus()
    {
        var r1 = MakeApprovalRequest(ApprovalStatus.Pending);
        var r2 = MakeApprovalRequest(ApprovalStatus.Approved);
        var list = new List<ApprovalRequest> { r1, r2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalRequestsQuery(
            TenantId: null,
            Status: "Approved",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllApprovalRequestsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.Equal("Approved", result.Value.Items[0].Status);
    }

    [Fact]
    public async Task GetAll_WithRejectedStatus_ReturnsDeniedLabel()
    {
        var r1 = MakeApprovalRequest(ApprovalStatus.Rejected);
        var list = new List<ApprovalRequest> { r1 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllApprovalRequestsQuery(
            TenantId: null,
            Status: "Denied",
            Search: null,
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllApprovalRequestsQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Denied", result.Value.Items[0].Status);
    }

    #endregion
}
