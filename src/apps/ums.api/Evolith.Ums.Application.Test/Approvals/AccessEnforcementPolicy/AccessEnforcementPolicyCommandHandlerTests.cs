namespace Evolith.Ums.Application.Test.Approvals.AccessEnforcementPolicy;

using System;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;
using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.AccessEnforcementPolicy;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class AccessEnforcementPolicyCommandHandlerTests
{
    private readonly Mock<IAccessEnforcementPolicyRepository> _repo = new();
    private readonly Mock<IUnitOfWork>                       _uow  = new();
    private readonly Mock<IUserContext>                      _ctx  = new();

    public AccessEnforcementPolicyCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
    }

    private static AccessEnforcementPolicy MakePolicy()
    {
        return AccessEnforcementPolicy.Create(
            TenantId.Load(Guid.NewGuid()),
            ProfileId.Load(Guid.NewGuid()),
            RoleId.Load(Guid.NewGuid()),
            AccessEnforcementAction.BlockUser,
            ActorId.Create("user-001")).Value;
    }

    // =========================================================================
    #region CreateAccessEnforcementPolicyCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");

        var cmd = new CreateAccessEnforcementPolicyCommand(
            TenantId: Guid.NewGuid(),
            ProfileId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            EnforcementAction: "BlockUser");

        var handler = new CreateAccessEnforcementPolicyCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.AccessEnforcementPolicyId);
        _repo.Verify(r => r.AddAsync(It.IsAny<AccessEnforcementPolicy>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreateAccessEnforcementPolicyCommand(
            TenantId: Guid.NewGuid(),
            ProfileId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            EnforcementAction: "BlockUser");

        var handler = new CreateAccessEnforcementPolicyCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenNoProfileOrRoleProvided_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");

        var cmd = new CreateAccessEnforcementPolicyCommand(
            TenantId: Guid.NewGuid(),
            ProfileId: null,
            RoleId: null,
            EnforcementAction: "BlockUser");

        var handler = new CreateAccessEnforcementPolicyCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region DeactivateAccessEnforcementPolicyCommandHandler
    // =========================================================================

    [Fact]
    public async Task Deactivate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var policy = MakePolicy();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(policy);

        var cmd = new DeactivateAccessEnforcementPolicyCommand(policy.Props.Id.GetValue());
        var handler = new DeactivateAccessEnforcementPolicyCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(policy.IsActive);
        _repo.Verify(r => r.UpdateAsync(policy, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AccessEnforcementPolicy?)null);

        var cmd = new DeactivateAccessEnforcementPolicyCommand(Guid.NewGuid());
        var handler = new DeactivateAccessEnforcementPolicyCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("policy not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivate_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new DeactivateAccessEnforcementPolicyCommand(Guid.NewGuid());
        var handler = new DeactivateAccessEnforcementPolicyCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deactivate_WhenAlreadyInactive_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var policy = MakePolicy();
        policy.Deactivate(ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(policy);

        var cmd = new DeactivateAccessEnforcementPolicyCommand(policy.Props.Id.GetValue());
        var handler = new DeactivateAccessEnforcementPolicyCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region UpdateAccessEnforcementActionCommandHandler
    // =========================================================================

    [Fact]
    public async Task UpdateAction_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var policy = MakePolicy();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(policy);

        var cmd = new UpdateAccessEnforcementActionCommand(policy.Props.Id.GetValue(), "RestrictProfile");
        var handler = new UpdateAccessEnforcementActionCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(policy, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAction_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AccessEnforcementPolicy?)null);

        var cmd = new UpdateAccessEnforcementActionCommand(Guid.NewGuid(), "BlockUser");
        var handler = new UpdateAccessEnforcementActionCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("policy not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAction_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new UpdateAccessEnforcementActionCommand(Guid.NewGuid(), "BlockUser");
        var handler = new UpdateAccessEnforcementActionCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAction_WithInvalidAction_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var policy = MakePolicy();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(policy);

        var cmd = new UpdateAccessEnforcementActionCommand(policy.Props.Id.GetValue(), "InvalidAction");
        var handler = new UpdateAccessEnforcementActionCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Invalid action", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // Irreversibilidad (G-051): el handler debe propagar el rechazo del dominio cuando se
    // intenta actualizar una politica ya desactivada (estado terminal) y NO debe persistir.
    // No debilitar: si este test empieza a esperar exito, la invariante de irreversibilidad
    // se habra roto de nuevo.
    [Fact]
    public async Task UpdateAction_WhenPolicyInactive_ReturnsFailureAndDoesNotPersist()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var policy = MakePolicy();
        policy.Deactivate(ActorId.Create("user-001"));
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(policy);

        var cmd = new UpdateAccessEnforcementActionCommand(policy.Props.Id.GetValue(), "RestrictProfile");
        var handler = new UpdateAccessEnforcementActionCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Approvals.PolicyInactiveCannotUpdate, result.Error);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<AccessEnforcementPolicy>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
