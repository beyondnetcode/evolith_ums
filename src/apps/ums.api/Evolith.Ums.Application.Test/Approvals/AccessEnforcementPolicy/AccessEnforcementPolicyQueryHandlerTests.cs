namespace Evolith.Ums.Application.Test.Approvals.AccessEnforcementPolicy;

using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Queries;
using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;
using Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class AccessEnforcementPolicyQueryHandlerTests
{
    private readonly Mock<IAccessEnforcementPolicyRepository> _repo = new();

    private static AccessEnforcementPolicy MakePolicy(AccessEnforcementAction action, bool isActive)
    {
        var policy = AccessEnforcementPolicy.Create(
            TenantId.Load(Guid.NewGuid()),
            ProfileId.Load(Guid.NewGuid()),
            RoleId.Load(Guid.NewGuid()),
            action,
            ActorId.Create("user-001")).Value;

        if (!isActive)
        {
            policy.Deactivate(ActorId.Create("user-001"));
        }

        return policy;
    }

    // =========================================================================
    #region GetAccessEnforcementPolicyByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsSuccess()
    {
        var policy = MakePolicy(AccessEnforcementAction.BlockUser, true);
        var policyId = policy.Props.Id.GetValue();
        _repo.Setup(r => r.GetByIdAsync(policyId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(policy);

        var query = new GetAccessEnforcementPolicyByIdQuery(policyId);
        var handler = new GetAccessEnforcementPolicyByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(policyId, result.Value.AccessEnforcementPolicyId);
        Assert.Equal("BlockUser", result.Value.EnforcementAction);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AccessEnforcementPolicy?)null);

        var query = new GetAccessEnforcementPolicyByIdQuery(Guid.NewGuid());
        var handler = new GetAccessEnforcementPolicyByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("policy not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetAllAccessEnforcementPoliciesQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_WithoutTenantFilter_ReturnsAllItems()
    {
        var p1 = MakePolicy(AccessEnforcementAction.BlockUser, true);
        var p2 = MakePolicy(AccessEnforcementAction.LogOnly, false);
        var list = new List<AccessEnforcementPolicy> { p1, p2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllAccessEnforcementPoliciesQuery(
            TenantId: null,
            Status: "all",
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllAccessEnforcementPoliciesQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalItems);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task GetAll_WithTenantFilter_ReturnsTenantItems()
    {
        var tenantId = Guid.NewGuid();
        var p1 = MakePolicy(AccessEnforcementAction.BlockUser, true);
        var list = new List<AccessEnforcementPolicy> { p1 };

        _repo.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllAccessEnforcementPoliciesQuery(
            TenantId: tenantId,
            Status: "all",
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var adminCtx = new Mock<Evolith.Ums.Application.Common.Interfaces.ITenantContext>();
        adminCtx.Setup(t => t.IsInternalAdmin).Returns(true);
        var handler = new GetAllAccessEnforcementPoliciesQueryHandler(_repo.Object, adminCtx.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        _repo.Verify(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WithActiveFilter_FiltersActiveOnly()
    {
        var p1 = MakePolicy(AccessEnforcementAction.BlockUser, true);
        var p2 = MakePolicy(AccessEnforcementAction.LogOnly, false);
        var list = new List<AccessEnforcementPolicy> { p1, p2 };

        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(list);

        var query = new GetAllAccessEnforcementPoliciesQuery(
            TenantId: null,
            Status: "active",
            SortBy: null,
            SortOrder: null,
            Page: 1,
            PageSize: 10);

        var handler = new GetAllAccessEnforcementPoliciesQueryHandler(_repo.Object);
        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalItems);
        Assert.True(result.Value.Items[0].IsActive);
    }

    #endregion
}
