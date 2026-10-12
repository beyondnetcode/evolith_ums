namespace Evolith.Ums.Application.Test.Authorization.SystemSuite;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Authorization.SystemSuite.Commands;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Authorization;
using Moq;
using Xunit;
using System;
using System.Threading;
using System.Threading.Tasks;

public class SystemSuiteCommandHandlerTests
{
    private readonly Mock<ISystemSuiteRepository> _repo = new();
    private readonly Mock<IUnitOfWork>             _uow  = new();
    private readonly Mock<IUserContext>            _ctx  = new();
    private readonly Mock<ITenantScopePolicy>      _scopePolicy = new();

    public SystemSuiteCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _scopePolicy.Setup(p => p.ResolveQueryScope()).Returns((Guid?)null);
    }

    private static SystemSuite MakeSystemSuite()
    {
        return SystemSuite.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create("SUITE-001"),
            Name.Create("Test Suite"),
            Description.Create("Test Suite Description"),
            ActorId.Create("user-001")).Value;
    }

    // =========================================================================
    #region CreateSystemSuiteCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<Code>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((SystemSuite?)null);
        _scopePolicy.Setup(p => p.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var cmd = new CreateSystemSuiteCommand(
            TenantId: Guid.NewGuid(),
            Code: "SUITE-001",
            Name: "Test Suite",
            Description: "Test Suite Description");

        var handler = new CreateSystemSuiteCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.SystemSuiteId);
        _repo.Verify(r => r.AddAsync(It.IsAny<SystemSuite>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenCodeAlreadyExists_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<Code>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeSystemSuite());
        _scopePolicy.Setup(p => p.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var cmd = new CreateSystemSuiteCommand(
            TenantId: Guid.NewGuid(),
            Code: "SUITE-001",
            Name: "Test Suite",
            Description: "Test Suite Description");

        var handler = new CreateSystemSuiteCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("code already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");
        _scopePolicy.Setup(p => p.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var cmd = new CreateSystemSuiteCommand(
            TenantId: Guid.NewGuid(),
            Code: "SUITE-001",
            Name: "Test Suite",
            Description: "Test Suite Description");

        var handler = new CreateSystemSuiteCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Create_WhenTenantIsNotManagementOwner_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByCodeAsync(It.IsAny<Code>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((SystemSuite?)null);
        _scopePolicy.Setup(p => p.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));

        var cmd = new CreateSystemSuiteCommand(
            TenantId: Guid.NewGuid(),
            Code: "SUITE-001",
            Name: "Test Suite",
            Description: "Test Suite Description");

        var handler = new CreateSystemSuiteCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("management owner", result.Error, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<SystemSuite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    // =========================================================================
    #region SetSystemSuiteStatusCommandHandler
    // =========================================================================

    [Fact]
    public async Task SetStatus_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var suite = MakeSystemSuite();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(suite);

        var cmd = new SetSystemSuiteStatusCommand(Guid.NewGuid(), "Maintenance");
        var handler = new SetSystemSuiteStatusCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Maintenance", suite.Status.Name);
        _repo.Verify(r => r.UpdateAsync(suite, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetStatus_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((SystemSuite?)null);

        var cmd = new SetSystemSuiteStatusCommand(Guid.NewGuid(), "Maintenance");
        var handler = new SetSystemSuiteStatusCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SetStatus_WhenInvalidStatus_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var suite = MakeSystemSuite();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(suite);

        var cmd = new SetSystemSuiteStatusCommand(Guid.NewGuid(), "InvalidStatusName");
        var handler = new SetSystemSuiteStatusCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("invalid system status", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region UpdateSystemSuiteCommandHandler
    // =========================================================================

    [Fact]
    public async Task Update_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var suite = MakeSystemSuite();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(suite);

        var cmd = new UpdateSystemSuiteCommand(Guid.NewGuid(), "New Name", "New Description");
        var handler = new UpdateSystemSuiteCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", suite.Name.GetValue());
        Assert.Equal("New Description", suite.Description.GetValue());
        _repo.Verify(r => r.UpdateAsync(suite, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((SystemSuite?)null);

        var cmd = new UpdateSystemSuiteCommand(Guid.NewGuid(), "New Name", "New Description");
        var handler = new UpdateSystemSuiteCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion
}
