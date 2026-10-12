namespace Evolith.Ums.Application.Test.Authorization.Role;

using Evolith.Ums.Application.Authorization.Role.Commands;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Kernel;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;
using Moq;
using Xunit;

public sealed class RoleCommandHandlerTests
{
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IProfileRepository> _profiles = new();
    private readonly Mock<ISystemSuiteRepository> _suites = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ITenantScopePolicy> _scopePolicy = new();

    public RoleCommandHandlerTests()
    {
        _roles.Setup(x => x.UnitOfWork).Returns(_unitOfWork.Object);
        _unitOfWork.Setup(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userContext.Setup(x => x.UserId).Returns("user-001");
        _scopePolicy.Setup(x => x.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Default: no active profiles or child roles → guard passes
        _profiles.Setup(x => x.CountActiveByRoleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _roles.Setup(x => x.CountActiveChildRolesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private SetRoleStatusCommandHandler MakeSetStatusHandler()
        => new(_roles.Object, _profiles.Object, _userContext.Object);

    private static SystemSuite MakeSuite() => SystemSuite.Create(
        TenantId.Load(Guid.NewGuid()),
        Code.Create("WMS"),
        Name.Create("Warehouse Management"),
        Description.Create("Warehouse"),
        ActorId.Create("user-001")).Value;

    [Fact]
    public async Task Create_WithUniqueRootRole_ReturnsRoleId()
    {
        var suite = MakeSuite();
        _suites.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(suite);
        _roles.Setup(x => x.GetByCodeAsync(It.IsAny<Guid>(), It.IsAny<Code>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoleAggregate?)null);
        var handler = new CreateRoleCommandHandler(_roles.Object, _suites.Object, _userContext.Object, _scopePolicy.Object);

        var result = await handler.Handle(new CreateRoleCommand(
            suite.GetId().GetValue(), "SECURITY_ADMIN", "Security Administrator", "Manages security", null, 0, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.RoleId);
        _roles.Verify(x => x.AddAsync(It.IsAny<RoleAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsHumanReadableCause()
    {
        var suite = MakeSuite();
        var existing = RoleAggregate.Create(
            suite.TenantId, suite.GetId(), Code.Create("SECURITY_ADMIN"), Name.Create("Admin"),
            Description.Create(""), null, 0, 0, ActorId.Create("user-001")).Value;
        _suites.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(suite);
        _roles.Setup(x => x.GetByCodeAsync(It.IsAny<Guid>(), It.IsAny<Code>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new CreateRoleCommandHandler(_roles.Object, _suites.Object, _userContext.Object, _scopePolicy.Object);

        var result = await handler.Handle(new CreateRoleCommand(
            suite.GetId().GetValue(), "SECURITY_ADMIN", "Other Admin", "", null, 0, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Código ya existe", result.Error);
        Assert.Contains("SECURITY_ADMIN", result.Error);
    }

    [Fact]
    public async Task Create_WhenTenantIsNotManagementOwner_ReturnsFailure()
    {
        var suite = MakeSuite();
        _suites.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(suite);
        _roles.Setup(x => x.GetByCodeAsync(It.IsAny<Guid>(), It.IsAny<Code>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoleAggregate?)null);
        _scopePolicy.Setup(x => x.EnsureManagementOwnerScopeAsync(suite.TenantId.GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));
        var handler = new CreateRoleCommandHandler(_roles.Object, _suites.Object, _userContext.Object, _scopePolicy.Object);

        var result = await handler.Handle(new CreateRoleCommand(
            suite.GetId().GetValue(), "SECURITY_ADMIN", "Security Administrator", "Manages security", null, 0, 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("management owner", result.Error, StringComparison.OrdinalIgnoreCase);
        _roles.Verify(x => x.AddAsync(It.IsAny<RoleAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetStatus_Deactivate_UpdatesRole()
    {
        var suite = MakeSuite();
        var role = RoleAggregate.Create(
            suite.TenantId, suite.GetId(), Code.Create("OPERATOR"), Name.Create("Operator"),
            Description.Create(""), null, 0, 0, ActorId.Create("user-001")).Value;
        _roles.Setup(x => x.GetByIdAsync(role.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(role);
        var handler = MakeSetStatusHandler();

        var result = await handler.Handle(new SetRoleStatusCommand(role.GetId().GetValue(), false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(role.IsActive);
        _roles.Verify(x => x.UpdateAsync(role, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetStatus_Deactivate_WhenRoleHasActiveProfiles_ReturnsBlockedFailure()
    {
        var suite = MakeSuite();
        var role = RoleAggregate.Create(
            suite.TenantId, suite.GetId(), Code.Create("OPERATOR"), Name.Create("Operator"),
            Description.Create(""), null, 0, 0, ActorId.Create("user-001")).Value;
        _roles.Setup(x => x.GetByIdAsync(role.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _profiles.Setup(x => x.CountActiveByRoleAsync(role.GetId().GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        var handler = MakeSetStatusHandler();

        var result = await handler.Handle(new SetRoleStatusCommand(role.GetId().GetValue(), false), CancellationToken.None);

        Assert.True(result.IsFailure);
        var decoded = BlockedOperationError.TryDecode(result.Error, out var code, out var deps);
        Assert.True(decoded);
        Assert.Equal(DomainErrors.Authorization.RoleHasActiveProfiles, code);
        Assert.Single(deps);
        Assert.Equal("Profile", deps[0].EntityType);
        Assert.Equal(5, deps[0].Count);
    }

    [Fact]
    public async Task SetStatus_Deactivate_WhenRoleHasActiveChildRoles_ReturnsBlockedFailure()
    {
        var suite = MakeSuite();
        var role = RoleAggregate.Create(
            suite.TenantId, suite.GetId(), Code.Create("OPERATOR"), Name.Create("Operator"),
            Description.Create(""), null, 0, 0, ActorId.Create("user-001")).Value;
        _roles.Setup(x => x.GetByIdAsync(role.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _roles.Setup(x => x.CountActiveChildRolesAsync(role.GetId().GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var handler = MakeSetStatusHandler();

        var result = await handler.Handle(new SetRoleStatusCommand(role.GetId().GetValue(), false), CancellationToken.None);

        Assert.True(result.IsFailure);
        var decoded = BlockedOperationError.TryDecode(result.Error, out var code, out _);
        Assert.True(decoded);
        Assert.Equal(DomainErrors.Authorization.RoleHasActiveChildRoles, code);
    }

    [Fact]
    public async Task SetStatus_Activate_DoesNotCheckDependencies()
    {
        var suite = MakeSuite();
        var role = RoleAggregate.Create(
            suite.TenantId, suite.GetId(), Code.Create("OPERATOR"), Name.Create("Operator"),
            Description.Create(""), null, 0, 0, ActorId.Create("user-001")).Value;
        role.Deactivate(ActorId.Create("user-001"));
        _roles.Setup(x => x.GetByIdAsync(role.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(role);
        // Setup unreachable: profiles have many active — should NOT be checked on activate
        _profiles.Setup(x => x.CountActiveByRoleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);
        var handler = MakeSetStatusHandler();

        var result = await handler.Handle(new SetRoleStatusCommand(role.GetId().GetValue(), true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _profiles.Verify(x => x.CountActiveByRoleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WhenParentIsDescendant_RejectsCycle()
    {
        var suite = MakeSuite();
        var root = RoleAggregate.Create(
            suite.TenantId, suite.GetId(), Code.Create("ROOT"), Name.Create("Root"),
            Description.Create(""), null, 0, 0, ActorId.Create("user-001")).Value;
        var child = RoleAggregate.Create(
            suite.TenantId, suite.GetId(), Code.Create("CHILD"), Name.Create("Child"),
            Description.Create(""), root.GetId(), 1, 1, ActorId.Create("user-001")).Value;
        _roles.Setup(x => x.GetByIdAsync(root.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(root);
        _roles.Setup(x => x.GetByIdAsync(child.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(child);
        var handler = new UpdateRoleCommandHandler(_roles.Object, _userContext.Object);

        var result = await handler.Handle(new UpdateRoleCommand(
            root.GetId().GetValue(), "Root", "", child.GetId().GetValue(), 2, 0),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("ciclo", result.Error);
    }
}
