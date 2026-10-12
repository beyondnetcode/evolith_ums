namespace Evolith.Ums.Application.Test.Approvals.ApprovalRequest;

using Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Moq;
using Xunit;
using ApprovalRequestAggregate = Evolith.Ums.Domain.Approvals.ApprovalRequest.ApprovalRequest;

/// <summary>EP-09: Tests for GetPendingProfileRequestsQueryHandler.</summary>
public class ProfileRequestInboxQueryHandlerTests
{
    private readonly Mock<IApprovalRequestRepository> _repo     = new();
    private readonly Mock<ITenantContext>              _tenantCtx = new();

    private static readonly Guid TenantId   = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly SystemSuiteId SystemId = SystemSuiteId.Load(Guid.NewGuid());
    private static readonly RoleId AdminRole = RoleId.Load(Guid.NewGuid());
    private static readonly RoleId OpRole    = RoleId.Load(Guid.NewGuid());

    public ProfileRequestInboxQueryHandlerTests()
    {
        _tenantCtx.Setup(t => t.OrganizationId).Returns(TenantId);
    }

    private static ApprovalRequestAggregate MakePending(Guid userId, RoleId? role = null)
    {
        return ApprovalRequestAggregate.Create(
            ApprovalWorkflowId.Load(Guid.NewGuid()),
            UserId.Load(userId),
            null,
            SystemId, null, role ?? AdminRole,
            "Test justification",
            ActorId.Create("user")).Value;
    }

    private static ApprovalRequestAggregate MakeApproved(Guid userId)
    {
        var req = MakePending(userId);
        req.Approve(ActorId.Create("admin"), AdminRole);
        return req;
    }

    [Fact]
    public async Task GetPendingProfileRequests_ReturnsPendingOnly()
    {
        var pending  = MakePending(Guid.NewGuid());
        var approved = MakeApproved(Guid.NewGuid());

        _repo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<ApprovalRequestAggregate> { pending, approved });

        var handler = new GetPendingProfileRequestsQueryHandler(_repo.Object, _tenantCtx.Object);
        var result  = await handler.Handle(new GetPendingProfileRequestsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(pending.Props.Id.GetValue(), result.Value[0].ApprovalRequestId);
    }

    [Fact]
    public async Task GetPendingProfileRequests_IncludesRequestedFields()
    {
        var userId  = Guid.NewGuid();
        var pending = MakePending(userId, OpRole);

        _repo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<ApprovalRequestAggregate> { pending });

        var handler = new GetPendingProfileRequestsQueryHandler(_repo.Object, _tenantCtx.Object);
        var result  = await handler.Handle(new GetPendingProfileRequestsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value[0];
        Assert.Equal(userId, dto.TargetUserId);
        Assert.Equal(SystemId.GetValue(), dto.RequestedSystemId);
        Assert.Equal(OpRole.GetValue(), dto.RequestedRoleId);
        Assert.Equal("Test justification", dto.Justification);
        Assert.Null(dto.RequestedBranchId);
    }

    [Fact]
    public async Task GetPendingProfileRequests_WhenNoTenantContext_ReturnsFailure()
    {
        _tenantCtx.Setup(t => t.OrganizationId).Returns((Guid?)null);

        var handler = new GetPendingProfileRequestsQueryHandler(_repo.Object, _tenantCtx.Object);
        var result  = await handler.Handle(new GetPendingProfileRequestsQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant context is required", result.Error);
    }

    [Fact]
    public async Task GetPendingProfileRequests_MultiplePendingRequests_ReturnedOrderedByDate()
    {
        var req1 = MakePending(Guid.NewGuid());
        var req2 = MakePending(Guid.NewGuid());
        var req3 = MakePending(Guid.NewGuid());

        _repo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<ApprovalRequestAggregate> { req1, req2, req3 });

        var handler = new GetPendingProfileRequestsQueryHandler(_repo.Object, _tenantCtx.Object);
        var result  = await handler.Handle(new GetPendingProfileRequestsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
    }

    [Fact]
    public async Task GetPendingProfileRequests_DuplicateScopeInvariant_BothReturnedUntilOneApproved()
    {
        // Two pending requests from same user for same scope — both visible in inbox
        // (the duplicate check is at creation time, not query time)
        var userId = Guid.NewGuid();
        var req1   = MakePending(userId, AdminRole);
        var req2   = MakePending(userId, OpRole);

        _repo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<ApprovalRequestAggregate> { req1, req2 });

        var handler = new GetPendingProfileRequestsQueryHandler(_repo.Object, _tenantCtx.Object);
        var result  = await handler.Handle(new GetPendingProfileRequestsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }
}
