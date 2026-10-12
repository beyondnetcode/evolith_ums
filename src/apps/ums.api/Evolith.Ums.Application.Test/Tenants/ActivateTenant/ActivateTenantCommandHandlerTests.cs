namespace Evolith.Ums.Application.Test.Tenants.ActivateTenant;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.Commands;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Kernel;

public class ActivateTenantCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _tenantRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserContext> _userContextMock;
    private readonly ActivateTenantCommandHandler _handler;

    public ActivateTenantCommandHandlerTests()
    {
        _tenantRepositoryMock = new Mock<ITenantRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _userContextMock = new Mock<IUserContext>();
        _handler = new ActivateTenantCommandHandler(_tenantRepositoryMock.Object, _userContextMock.Object);
    }

    #region Handle - Success Scenarios

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateSuspendedTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await _handler.Handle(new ActivateTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesTenant()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateSuspendedTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        await _handler.Handle(new ActivateTenantCommand(tenantId), CancellationToken.None);

        _tenantRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Tenant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsSaveEntities()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateSuspendedTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _tenantRepositoryMock.Setup(r => r.UnitOfWork).Returns(unitOfWorkMock.Object);

        await _handler.Handle(new ActivateTenantCommand(tenantId), CancellationToken.None);

        unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Handle - Failure Scenarios

    [Fact]
    public async Task Handle_WhenUserIdIsEmpty_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("");

        var result = await _handler.Handle(new ActivateTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserIdIsNull_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns((string?)null);

        var result = await _handler.Handle(new ActivateTenantCommand(tenantId), CancellationToken.None);

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

        var result = await _handler.Handle(new ActivateTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant was not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenTenantAlreadyActive_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _userContextMock.Setup(u => u.UserId).Returns("user-001");
        var tenant = CreateActiveTenant();
        _tenantRepositoryMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await _handler.Handle(new ActivateTenantCommand(tenantId), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    private static Tenant CreateSuspendedTenant()
    {
        var tenant = Tenant.Create(
            Code.Create("TEST-001"),
            Name.Create("Test Tenant"),
            OrganizationType.INTERNAL,
            ActorId.Create("user-001"),
            IdpStrategy.InternalBcrypt,
            null,
            null).Value;
        tenant.Suspend(ActorId.Create("user-001"));
        return tenant;
    }

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
