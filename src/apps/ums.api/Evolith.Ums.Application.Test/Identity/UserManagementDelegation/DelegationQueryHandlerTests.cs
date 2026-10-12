namespace Evolith.Ums.Application.Test.Identity.UserManagementDelegation;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.UserManagementDelegation.Queries;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class DelegationQueryHandlerTests
{
    private readonly Mock<IUserManagementDelegationRepository> _repo = new();
    private readonly Mock<IUnitOfWork>                           _uow  = new();

    public DelegationQueryHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
    }

    private static UserManagementDelegation MakeDelegation()
    {
        return UserManagementDelegation.Create(
            TenantId.Load(Guid.NewGuid()),
            UserAccountId.Load(Guid.NewGuid()),
            UserAccountId.Load(Guid.NewGuid()),
            DelegationScopeType.Tenant,
            null,
            new List<DelegatedAction> { DelegatedAction.CreateUser },
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(5),
            10,
            false,
            ActorId.Create("user-001")).Value;
    }

    // =========================================================================
    #region GetAllDelegationsQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetAll_ReturnsAllDelegations()
    {
        var d1 = MakeDelegation();
        var d2 = MakeDelegation();
        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UserManagementDelegation> { d1, d2 });

        var handler = new GetAllDelegationsQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetAllDelegationsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        _repo.Verify(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsEmptyList()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UserManagementDelegation>());

        var handler = new GetAllDelegationsQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetAllDelegationsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    #endregion

    // =========================================================================
    #region GetDelegationByIdQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetById_WhenFound_ReturnsDto()
    {
        var delegation = MakeDelegation();
        var id = delegation.GetId().GetValue();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var handler = new GetDelegationByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetDelegationByIdQuery(id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value.DelegationId);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserManagementDelegation?)null);

        var handler = new GetDelegationByIdQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetDelegationByIdQuery(id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region GetDelegationsByDelegatingAdminQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetByDelegatingAdmin_ReturnsMatchingDelegations()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var d1 = MakeDelegation();
        _repo.Setup(r => r.GetByDelegatingAdminAsync(adminId, tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UserManagementDelegation> { d1 });

        var handler = new GetDelegationsByDelegatingAdminQueryHandler(_repo.Object);
        var result = await handler.Handle(
            new GetDelegationsByDelegatingAdminQuery(adminId, tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        _repo.Verify(r => r.GetByDelegatingAdminAsync(adminId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByDelegatingAdmin_WhenNone_ReturnsEmptyList()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByDelegatingAdminAsync(adminId, tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UserManagementDelegation>());

        var handler = new GetDelegationsByDelegatingAdminQueryHandler(_repo.Object);
        var result = await handler.Handle(
            new GetDelegationsByDelegatingAdminQuery(adminId, tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    #endregion

    // =========================================================================
    #region GetDelegationsByDelegatedAdminQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetByDelegatedAdmin_ReturnsMatchingDelegations()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var d1 = MakeDelegation();
        _repo.Setup(r => r.GetByDelegatedAdminAsync(adminId, tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UserManagementDelegation> { d1 });

        var handler = new GetDelegationsByDelegatedAdminQueryHandler(_repo.Object);
        var result = await handler.Handle(
            new GetDelegationsByDelegatedAdminQuery(adminId, tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        _repo.Verify(r => r.GetByDelegatedAdminAsync(adminId, tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByDelegatedAdmin_WhenNone_ReturnsEmptyList()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _repo.Setup(r => r.GetByDelegatedAdminAsync(adminId, tenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UserManagementDelegation>());

        var handler = new GetDelegationsByDelegatedAdminQueryHandler(_repo.Object);
        var result = await handler.Handle(
            new GetDelegationsByDelegatedAdminQuery(adminId, tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    #endregion
}
