namespace Evolith.Ums.Application.Test.Identity.UserManagementDelegation;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class DelegationCommandHandlerTests
{
    private readonly Mock<IUserManagementDelegationRepository> _repo = new();
    private readonly Mock<IUserAccountRepository> _userRepo = new();
    private readonly Mock<IUnitOfWork>                           _uow  = new();
    private readonly Mock<IUserContext>                          _ctx  = new();
    private readonly Mock<ITenantScopePolicy>                    _scope = new();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _delegatingAdminId;
    private readonly Guid _delegatedAdminId = Guid.NewGuid();

    public DelegationCommandHandlerTests()
    {
        _delegatingAdminId = _currentUserId;
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns(_currentUserId.ToString());
        // Por defecto el operador es management-owner del inquilino objetivo (gate satisfecho).
        _scope.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result.Success());
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

    // Delegación con delegatingAdmin conocido, en PendingApproval — para probar la SoD (auto-aprobación).
    private static UserManagementDelegation MakePendingApprovalDelegationWith(Guid delegatingAdminId)
    {
        var delegation = UserManagementDelegation.Create(
            TenantId.Load(Guid.NewGuid()),
            UserAccountId.Load(delegatingAdminId),
            UserAccountId.Load(Guid.NewGuid()),
            DelegationScopeType.Tenant,
            null,
            new List<DelegatedAction> { DelegatedAction.CreateUser },
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(5),
            10,
            true,
            ActorId.Create(delegatingAdminId.ToString())).Value;
        delegation.SubmitForApproval(Guid.NewGuid(), ActorId.Create(delegatingAdminId.ToString()));
        return delegation;
    }

    private UserAccount MakeActiveUser(Guid userId)
    {
        var user = UserAccount.Create(
            TenantId.Load(_tenantId),
            Email.Create($"{userId:N}@test.com"),
            Evolith.Ums.Domain.Enums.UserCategory.Internal,
            null,
            null,
            ActorId.Create("user-001"),
            null,
            UserAccountId.Load(userId)).Value;
        user.Activate(ActorId.Create("user-001"));
        return user;
    }

    // =========================================================================
    #region CreateDelegationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns(_currentUserId.ToString());
        _userRepo.Setup(r => r.GetByIdAsync(_delegatingAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeActiveUser(_delegatingAdminId));
        _userRepo.Setup(r => r.GetByIdAsync(_delegatedAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeActiveUser(_delegatedAdminId));

        var cmd = new CreateDelegationCommand(
            TenantId: _tenantId,
            DelegatingAdminId: _delegatingAdminId,
            DelegatedAdminId: _delegatedAdminId,
            ScopeType: "Tenant",
            ScopeId: null,
            AllowedActions: new[] { "CreateUser" },
            ValidFrom: DateTimeOffset.UtcNow,
            ValidUntil: DateTimeOffset.UtcNow.AddDays(5),
            MaxDurationDays: 10,
            RequiresApproval: false);

        var handler = new CreateDelegationCommandHandler(_repo.Object, _userRepo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.DelegationId);
        _repo.Verify(r => r.AddAsync(It.IsAny<UserManagementDelegation>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenInvalidScopeType_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns(_currentUserId.ToString());
        _userRepo.Setup(r => r.GetByIdAsync(_delegatingAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeActiveUser(_delegatingAdminId));
        _userRepo.Setup(r => r.GetByIdAsync(_delegatedAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeActiveUser(_delegatedAdminId));

        var cmd = new CreateDelegationCommand(
            TenantId: Guid.NewGuid(),
            DelegatingAdminId: _delegatingAdminId,
            DelegatedAdminId: Guid.NewGuid(),
            ScopeType: "InvalidScope",
            ScopeId: null,
            AllowedActions: new[] { "CreateUser" },
            ValidFrom: DateTimeOffset.UtcNow,
            ValidUntil: DateTimeOffset.UtcNow.AddDays(5),
            MaxDurationDays: 10,
            RequiresApproval: false);

        var handler = new CreateDelegationCommandHandler(_repo.Object, _userRepo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("invalid scope type", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenEmptyAllowedActions_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns(_currentUserId.ToString());
        _userRepo.Setup(r => r.GetByIdAsync(_delegatingAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeActiveUser(_delegatingAdminId));
        _userRepo.Setup(r => r.GetByIdAsync(_delegatedAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeActiveUser(_delegatedAdminId));

        var cmd = new CreateDelegationCommand(
            TenantId: _tenantId,
            DelegatingAdminId: _delegatingAdminId,
            DelegatedAdminId: _delegatedAdminId,
            ScopeType: "Tenant",
            ScopeId: null,
            AllowedActions: new string[] { },
            ValidFrom: DateTimeOffset.UtcNow,
            ValidUntil: DateTimeOffset.UtcNow.AddDays(5),
            MaxDurationDays: 10,
            RequiresApproval: false);

        var handler = new CreateDelegationCommandHandler(_repo.Object, _userRepo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("at least one valid allowed action is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreateDelegationCommand(
            TenantId: Guid.NewGuid(),
            DelegatingAdminId: Guid.NewGuid(),
            DelegatedAdminId: Guid.NewGuid(),
            ScopeType: "Tenant",
            ScopeId: null,
            AllowedActions: new[] { "CreateUser" },
            ValidFrom: DateTimeOffset.UtcNow,
            ValidUntil: DateTimeOffset.UtcNow.AddDays(5),
            MaxDurationDays: 10,
            RequiresApproval: false);

        var handler = new CreateDelegationCommandHandler(_repo.Object, _userRepo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region ActivateDelegationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Activate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakeDelegation(); // Born Draft / Pending status
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new ActivateDelegationCommand(Guid.NewGuid());
        var handler = new ActivateDelegationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Active, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(delegation, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Activate_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserManagementDelegation?)null);

        var cmd = new ActivateDelegationCommand(Guid.NewGuid());
        var handler = new ActivateDelegationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region RevokeDelegationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Revoke_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakeDelegation();
        delegation.Activate(ActorId.Create("user-001")); // set Active

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new RevokeDelegationCommand(Guid.NewGuid(), "Admin action");
        var handler = new RevokeDelegationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Revoked, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(delegation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserManagementDelegation?)null);

        var cmd = new RevokeDelegationCommand(Guid.NewGuid(), "Reason");
        var handler = new RevokeDelegationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region ExpireDelegationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Expire_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakeDelegation();
        delegation.Activate(ActorId.Create("user-001")); // set Active

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new ExpireDelegationCommand(Guid.NewGuid());
        var handler = new ExpireDelegationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Expired, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(delegation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Expire_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserManagementDelegation?)null);

        var cmd = new ExpireDelegationCommand(Guid.NewGuid());
        var handler = new ExpireDelegationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region SubmitDelegationForApprovalCommandHandler (G-132)
    // =========================================================================

    private static UserManagementDelegation MakePendingApprovalDelegation()
    {
        var delegation = MakeDelegation(); // Born Draft
        delegation.SubmitForApproval(Guid.NewGuid(), ActorId.Create("user-001"));
        return delegation;
    }

    [Fact]
    public async Task Submit_WithDraftDelegation_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakeDelegation(); // Born Draft
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new SubmitDelegationForApprovalCommand(Guid.NewGuid());
        var handler = new SubmitDelegationForApprovalCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.PendingApproval, delegation.Status);
        Assert.NotNull(delegation.ApprovalRequestId);
        Assert.NotEqual(Guid.Empty, delegation.ApprovalRequestId!.Value);
        _repo.Verify(r => r.UpdateAsync(delegation, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_WhenNotDraft_ReturnsFailure()
    {
        // Camino de RECHAZO: enviar a aprobación fuera de estado Draft (ya en PendingApproval) → falla.
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakePendingApprovalDelegation();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new SubmitDelegationForApprovalCommand(Guid.NewGuid());
        var handler = new SubmitDelegationForApprovalCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UserManagementDelegation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserManagementDelegation?)null);

        var cmd = new SubmitDelegationForApprovalCommand(Guid.NewGuid());
        var handler = new SubmitDelegationForApprovalCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new SubmitDelegationForApprovalCommand(Guid.NewGuid());
        var handler = new SubmitDelegationForApprovalCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    // =========================================================================
    #region ApproveDelegationCommandHandler (G-132)
    // =========================================================================

    [Fact]
    public async Task Approve_WithPendingApproval_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakePendingApprovalDelegation();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new ApproveDelegationCommand(Guid.NewGuid());
        var handler = new ApproveDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Active, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(delegation, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_WhenNotPendingApproval_ReturnsFailure()
    {
        // Camino de RECHAZO: aprobar (activar) una delegación en Draft, fuera de PendingApproval → falla.
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakeDelegation(); // Born Draft
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new ApproveDelegationCommand(Guid.NewGuid());
        var handler = new ApproveDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DelegationStatus.Draft, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UserManagementDelegation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserManagementDelegation?)null);

        var cmd = new ApproveDelegationCommand(Guid.NewGuid());
        var handler = new ApproveDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    // G-148 — AUTORIZACIÓN: sin autoridad de management-owner, aprobar falla y no persiste.
    [Fact]
    public async Task Approve_WhenNotManagementOwner_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakePendingApprovalDelegation();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);
        // El operador NO es management-owner del inquilino → gate rechaza.
        _scope.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));

        var cmd = new ApproveDelegationCommand(Guid.NewGuid());
        var handler = new ApproveDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DelegationStatus.PendingApproval, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UserManagementDelegation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // G-149 — AISLAMIENTO POR INQUILINO: operar sobre una delegación de otro inquilino falla
    // (el gate de management-owner devuelve AUTH_014 tenant mismatch) y no persiste.
    [Fact]
    public async Task Approve_WhenCrossTenant_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakePendingApprovalDelegation();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);
        _scope.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result.Failure("AUTH_014: Tenant mismatch."));

        var cmd = new ApproveDelegationCommand(Guid.NewGuid());
        var handler = new ApproveDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_014", result.Error, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UserManagementDelegation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // G-150 — SEPARACIÓN DE FUNCIONES: el administrador delegante no puede autoaprobar su propia
    // delegación, aun siendo management-owner (invariante de dominio INV-DEL8). No persiste.
    [Fact]
    public async Task Approve_WhenSelfApproval_ReturnsFailure()
    {
        var delegatingAdminId = Guid.NewGuid();
        _ctx.Setup(u => u.UserId).Returns(delegatingAdminId.ToString());
        var delegation = MakePendingApprovalDelegationWith(delegatingAdminId);
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);
        // El gate de autorización se satisface (es management-owner); solo la SoD debe frenarlo.
        _scope.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Result.Success());

        var cmd = new ApproveDelegationCommand(Guid.NewGuid());
        var handler = new ApproveDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DelegationStatus.PendingApproval, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UserManagementDelegation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    // =========================================================================
    #region RejectDelegationCommandHandler (G-132)
    // =========================================================================

    [Fact]
    public async Task Reject_WithPendingApproval_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakePendingApprovalDelegation();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new RejectDelegationCommand(Guid.NewGuid(), "Insufficient justification");
        var handler = new RejectDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DelegationStatus.Rejected, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(delegation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_WhenNotPendingApproval_ReturnsFailure()
    {
        // Camino de RECHAZO: rechazar una delegación en Draft, fuera de PendingApproval → falla.
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakeDelegation(); // Born Draft
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new RejectDelegationCommand(Guid.NewGuid(), "reason");
        var handler = new RejectDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DelegationStatus.Draft, delegation.Status);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UserManagementDelegation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reject_WhenEmptyReason_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var delegation = MakePendingApprovalDelegation();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(delegation);

        var cmd = new RejectDelegationCommand(Guid.NewGuid(), "   ");
        var handler = new RejectDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(DelegationStatus.PendingApproval, delegation.Status);
    }

    [Fact]
    public async Task Reject_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserManagementDelegation?)null);

        var cmd = new RejectDelegationCommand(Guid.NewGuid(), "reason");
        var handler = new RejectDelegationCommandHandler(_repo.Object, _ctx.Object, _scope.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion
}
