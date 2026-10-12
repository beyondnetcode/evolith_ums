namespace Evolith.Ums.Application.Test.Identity.UserAccount;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Application.Identity.UserAccount.Commands;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Enums;
using Moq;
using Xunit;
using System;
using System.Threading;
using System.Threading.Tasks;

public class UserAccountCommandHandlerTests
{
    private readonly Mock<IUserAccountRepository> _repo = new();
    private readonly Mock<IUnitOfWork>            _uow  = new();
    private readonly Mock<IUserContext>            _ctx  = new();
    private readonly Mock<IProfileRepository>      _profileRepo = new();
    private readonly Mock<ITenantScopePolicy>      _scopePolicy = new();
    private readonly Mock<IUserManagementDelegationAccessService> _delegationAccess = new();
    private readonly Mock<ITenantRepository>       _tenantRepo = new();
    private readonly Mock<INotificationService>    _notifications = new();
    private readonly Mock<IPasswordHashingService> _passwordHashing = new();

    public UserAccountCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _passwordHashing.Setup(s => s.Hash(It.IsAny<string>())).Returns("$2a$12$server-generated-hash");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _delegationAccess.Setup(s => s.EnsureCanExecuteAsync(It.IsAny<Guid>(), It.IsAny<Evolith.Ums.Domain.Identity.UserManagementDelegation.DelegatedAction>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("delegated access required"));
        _profileRepo.Setup(r => r.CountActiveByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<Evolith.Ums.Domain.Identity.Tenant.Tenant?>(null));
    }

    private static UserAccount MakeUserAccount()
    {
        return UserAccount.Create(
            TenantId.Load(Guid.NewGuid()),
            Email.Create("test@test.com"),
            UserCategory.Internal,
            null,
            null,
            ActorId.Create("user-001"),
            null).Value;
    }

    // =========================================================================
    #region CreateUserAccountCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserAccount?)null);

        var cmd = new CreateUserAccountCommand(
            TenantId: Guid.NewGuid(),
            BranchId: null,
            Email: "newuser@test.com",
            Category: "Internal",
            IdentityReference: null,
            IdentityReferenceType: null);

        var handler = new CreateUserAccountCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.UserAccountId);
        _repo.Verify(r => r.AddAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenEmailAlreadyExists_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeUserAccount());

        var cmd = new CreateUserAccountCommand(
            TenantId: Guid.NewGuid(),
            BranchId: null,
            Email: "test@test.com",
            Category: "Internal",
            IdentityReference: null,
            IdentityReferenceType: null);

        var handler = new CreateUserAccountCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("email already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreateUserAccountCommand(
            TenantId: Guid.NewGuid(),
            BranchId: null,
            Email: "newuser@test.com",
            Category: "Internal",
            IdentityReference: null,
            IdentityReferenceType: null);

        var handler = new CreateUserAccountCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenTenantIsNotManagementOwner_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));

        var cmd = new CreateUserAccountCommand(
            TenantId: Guid.NewGuid(),
            BranchId: null,
            Email: "newuser@test.com",
            Category: "Internal",
            IdentityReference: null,
            IdentityReferenceType: null);

        var handler = new CreateUserAccountCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("management owner", result.Error, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenDelegatedAccessIsGranted_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));
        _delegationAccess.Setup(s => s.EnsureCanExecuteAsync(
                It.IsAny<Guid>(),
                Evolith.Ums.Domain.Identity.UserManagementDelegation.DelegatedAction.CreateUser,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserAccount?)null);

        var cmd = new CreateUserAccountCommand(
            TenantId: Guid.NewGuid(),
            BranchId: null,
            Email: "delegated@test.com",
            Category: "Internal",
            IdentityReference: null,
            IdentityReferenceType: null);

        var handler = new CreateUserAccountCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.AddAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    // =========================================================================
    #region ActivateUserAccountCommandHandler
    // =========================================================================

    [Fact]
    public async Task Activate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var user = MakeUserAccount(); // born in Pending status
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new ActivateUserAccountCommand(Guid.NewGuid());
        var handler = new ActivateUserAccountCommandHandler(_repo.Object, _ctx.Object, _tenantRepo.Object, _notifications.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        _repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Activate_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserAccount?)null);

        var cmd = new ActivateUserAccountCommand(Guid.NewGuid());
        var handler = new ActivateUserAccountCommandHandler(_repo.Object, _ctx.Object, _tenantRepo.Object, _notifications.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activate_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new ActivateUserAccountCommand(Guid.NewGuid());
        var handler = new ActivateUserAccountCommandHandler(_repo.Object, _ctx.Object, _tenantRepo.Object, _notifications.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Activate_WhenAlreadyActive_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var user = MakeUserAccount();
        user.Activate(ActorId.Create("user-001")); // Make Active

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new ActivateUserAccountCommand(Guid.NewGuid());
        var handler = new ActivateUserAccountCommandHandler(_repo.Object, _ctx.Object, _tenantRepo.Object, _notifications.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region BlockUserAccountCommandHandler
    // =========================================================================

    [Fact]
    public async Task Block_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var user = MakeUserAccount();
        user.Activate(ActorId.Create("user-001")); // Needs to be Active or Pending
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new BlockUserAccountCommand(Guid.NewGuid(), "Security risk");
        var handler = new BlockUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Blocked, user.Status);
        _repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Block_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserAccount?)null);

        var cmd = new BlockUserAccountCommand(Guid.NewGuid(), "Reason");
        var handler = new BlockUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Block_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new BlockUserAccountCommand(Guid.NewGuid(), "Reason");
        var handler = new BlockUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Block_WhenAlreadyBlocked_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var user = MakeUserAccount();
        user.Block(Reason.Create("Reason"), ActorId.Create("user-001")); // already blocked

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new BlockUserAccountCommand(Guid.NewGuid(), "Reason");
        var handler = new BlockUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Block_WhenDelegatedAccessIsGranted_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var user = MakeUserAccount();
        user.Activate(ActorId.Create("user-001"));
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));
        _delegationAccess.Setup(s => s.EnsureCanExecuteAsync(
                It.IsAny<Guid>(),
                Evolith.Ums.Domain.Identity.UserManagementDelegation.DelegatedAction.BlockUser,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var cmd = new BlockUserAccountCommand(Guid.NewGuid(), "Delegated block");
        var handler = new BlockUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object, _scopePolicy.Object, _delegationAccess.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Blocked, user.Status);
    }

    #endregion

    // =========================================================================
    #region RestoreUserAccountCommandHandler
    // =========================================================================

    [Fact]
    public async Task Restore_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var user = MakeUserAccount();
        user.Block(Reason.Create("Reason"), ActorId.Create("user-001")); // set to Blocked first
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new RestoreUserAccountCommand(Guid.NewGuid());
        var handler = new RestoreUserAccountCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        _repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Restore_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserAccount?)null);

        var cmd = new RestoreUserAccountCommand(Guid.NewGuid());
        var handler = new RestoreUserAccountCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new RestoreUserAccountCommand(Guid.NewGuid());
        var handler = new RestoreUserAccountCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Restore_WhenNotBlocked_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var user = MakeUserAccount(); // born Pending, not Blocked

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new RestoreUserAccountCommand(Guid.NewGuid());
        var handler = new RestoreUserAccountCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    #region Password management

    [Fact]
    public async Task SetPassword_WithNativeAccount_HashesOnServerAndActivatesCredential()
    {
        var user = MakeUserAccount();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var handler = new AddUserAccountPasswordCommandHandler(_repo.Object, _ctx.Object, _passwordHashing.Object);
        var result = await handler.Handle(
            new AddUserAccountPasswordCommand(Guid.NewGuid(), "Temporary!Pass123"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(user.PasswordCredentials.Single().IsActive);
        Assert.Equal("$2a$12$server-generated-hash", user.ActivePasswordHash?.GetValue());
        _passwordHashing.Verify(s => s.Hash("Temporary!Pass123"), Times.Once);
        _repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPassword_WithFederatedAccount_ReturnsReadableFailureWithoutHashing()
    {
        var user = UserAccount.Create(
            TenantId.Load(Guid.NewGuid()),
            Email.Create("federated@test.com"),
            UserCategory.Internal,
            IdentityReference.Create("EMP-100"),
            IdentityReferenceType.HrId,
            ActorId.Create("user-001")).Value;
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var handler = new AddUserAccountPasswordCommandHandler(_repo.Object, _ctx.Object, _passwordHashing.Object);
        var result = await handler.Handle(
            new AddUserAccountPasswordCommand(Guid.NewGuid(), "Temporary!Pass123"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("autenticación federada", result.Error);
        _passwordHashing.Verify(s => s.Hash(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Delete (REC-16)

    [Fact]
    public async Task Delete_WhenValid_ReturnsSuccess()
    {
        var user = MakeUserAccount();
        user.Activate(ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);
        _repo.Setup(r => r.SoftDeleteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var cmd = new DeleteUserAccountCommand(Guid.NewGuid());
        var handler = new DeleteUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.SoftDeleteAsync(It.IsAny<Guid>(), "user-001", It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenUserNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserAccount?)null);

        var cmd = new DeleteUserAccountCommand(Guid.NewGuid());
        var handler = new DeleteUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_WhenAlreadyDeleted_ReturnsFailure()
    {
        var user = MakeUserAccount();
        user.Activate(ActorId.Create("user-001"));
        user.Delete(ActorId.Create("user-001")); // already deleted

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new DeleteUserAccountCommand(Guid.NewGuid());
        var handler = new DeleteUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Delete_WhenNoAuthenticatedUser_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new DeleteUserAccountCommand(Guid.NewGuid());
        var handler = new DeleteUserAccountCommandHandler(_repo.Object, _profileRepo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    #endregion
}
