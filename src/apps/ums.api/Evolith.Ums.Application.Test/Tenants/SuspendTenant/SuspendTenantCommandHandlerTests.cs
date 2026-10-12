namespace Evolith.Ums.Application.Test.Tenants.SuspendTenant;

using Evolith.Ums.Application.Identity.Tenant.Commands;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Kernel;

public class SuspendTenantCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _tenantRepositoryMock;
    private readonly Mock<IUserAccountRepository> _userAccountRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly SuspendTenantCommandHandler _handler;

    public SuspendTenantCommandHandlerTests()
    {
        _tenantRepositoryMock = new Mock<ITenantRepository>();
        _userAccountRepositoryMock = new Mock<IUserAccountRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _userContextMock = new Mock<IUserContext>();

        // Default: no active users → guard passes
        _userAccountRepositoryMock
            .Setup(r => r.CountActiveByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _handler = new SuspendTenantCommandHandler(
            _tenantRepositoryMock.Object,
            _userAccountRepositoryMock.Object,
            _userContextMock.Object);
    }

    #region Handle - Success Scenarios

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesTenant()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        _tenantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsSaveEntities()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(unitOfWorkMock.Object);

        await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Handle - Failure Scenarios

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("");

        var result = await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsNull_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns((string?)null);

        var result = await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenTenantNotFound_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var result = await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant was not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenTenantAlreadySuspended_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        tenant.Suspend(ActorId.Create("user-001"));
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    // ── Dependency guard tests ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTenantHasActiveUsers_ReturnsBlockedFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _userAccountRepositoryMock
            .Setup(r => r.CountActiveByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);

        var result = await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Tenant.HasActiveUsers, result.Error);
    }

    [Fact]
    public async Task Handle_WhenTenantHasActiveUsers_DoesNotUpdateTenant()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _userAccountRepositoryMock
            .Setup(r => r.CountActiveByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        _tenantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTenantHasActiveUsers_ErrorContainsBlockingDependencies()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _userAccountRepositoryMock
            .Setup(r => r.CountActiveByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        var decoded = BlockedOperationError.TryDecode(result.Error, out var code, out var deps);
        Assert.True(decoded);
        Assert.Equal(DomainErrors.Tenant.HasActiveUsers, code);
        Assert.Single(deps);
        Assert.Equal("UserAccount", deps[0].EntityType);
        Assert.Equal(3, deps[0].Count);
    }

    [Fact]
    public async Task Handle_WhenTenantHasNoActiveUsers_AllowsSuspension()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        _userAccountRepositoryMock
            .Setup(r => r.CountActiveByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.Handle(new SuspendTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    #endregion

    private static Tenant CreateActiveTenant()
    {
        return Tenant.Create(
            Code.Create("TEST-001"),
            Name.Create("Test Tenant"),
            OrganizationType.INTERNAL,
            ActorId.Create("user-001"),
            IdpStrategy.InternalBcrypt,
            null,
            null).Value;
    }
}
