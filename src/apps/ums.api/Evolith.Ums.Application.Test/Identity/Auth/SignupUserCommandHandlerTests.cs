namespace Evolith.Ums.Application.Test.Identity.Auth;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Application.Identity.Auth.Commands;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class SignupUserCommandHandlerTests
{
    private readonly Mock<ITenantRepository>       _tenantRepo     = new();
    private readonly Mock<IUserAccountRepository>  _userRepo       = new();
    private readonly Mock<IPasswordHashingService> _hasher         = new();
    private readonly Mock<INotificationService>    _notifications  = new();
    private readonly Mock<IUnitOfWork>             _uow            = new();

    private static readonly Guid   TenantId   = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private const           string TenantCode = "ACME";

    public SignupUserCommandHandlerTests()
    {
        _userRepo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
    }

    private static Domain.Identity.Tenant.Tenant MakeTenant() =>
        Domain.Identity.Tenant.Tenant.Create(
            Code.Create(TenantCode), Name.Create("Acme Corp"),
            OrganizationType.INTERNAL, ActorId.Create("sys"),
            tenantId: Domain.Kernel.ValueObjects.TenantId.Load(TenantId)).Value;

    private static UserAccount MakeActiveInternalAdmin()
    {
        var user = UserAccount.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(TenantId),
            Email.Create("admin@acme.com"),
            UserCategory.Internal,
            null, null,
            ActorId.Create("sys")).Value;
        user.Activate(ActorId.Create("sys"));
        return user;
    }

    private SignupUserCommandHandler CreateHandler() =>
        new(_tenantRepo.Object, _userRepo.Object, _hasher.Object, _notifications.Object);

    // =========================================================================
    #region Happy path
    // =========================================================================

    [Fact]
    public async Task Signup_WithValidRequest_ReturnsSuccess()
    {
        var tenant = MakeTenant();
        var admin  = MakeActiveInternalAdmin();

        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserAccount?)null);
        _userRepo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<UserAccount> { admin });

        var result = await CreateHandler().Handle(
            new SignupUserCommand(TenantCode, "Alice", "alice@example.com", "Str0ng!Pass"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.UserAccountId);
    }

    [Fact]
    public async Task Signup_WithValidRequest_PersistsUserAndSavesChanges()
    {
        var tenant = MakeTenant();
        var admin  = MakeActiveInternalAdmin();

        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserAccount?)null);
        _userRepo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<UserAccount> { admin });

        await CreateHandler().Handle(
            new SignupUserCommand(TenantCode, "Alice", "alice@example.com", "Str0ng!Pass"),
            CancellationToken.None);

        _userRepo.Verify(r => r.AddAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Signup_WithValidRequest_SendsTwoNotifications()
    {
        var tenant = MakeTenant();
        var admin  = MakeActiveInternalAdmin();

        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserAccount?)null);
        _userRepo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<UserAccount> { admin });

        await CreateHandler().Handle(
            new SignupUserCommand(TenantCode, "Alice", "alice@example.com", "Str0ng!Pass"),
            CancellationToken.None);

        // Expects UserSignupRequestReceived (to admin) + UserSignupConfirmation (to applicant)
        _notifications.Verify(
            n => n.SendBulkAsync(
                It.Is<IEnumerable<UmsNotification>>(list => list.Count() == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Signup_WithValidRequest_AdminReceivesRequestNotification()
    {
        var tenant = MakeTenant();
        var admin  = MakeActiveInternalAdmin();

        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserAccount?)null);
        _userRepo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<UserAccount> { admin });

        await CreateHandler().Handle(
            new SignupUserCommand(TenantCode, "Alice", "alice@example.com", "Str0ng!Pass"),
            CancellationToken.None);

        _notifications.Verify(
            n => n.SendBulkAsync(
                It.Is<IEnumerable<UmsNotification>>(list =>
                    list.Any(n => n.Recipient == "admin@acme.com")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    // =========================================================================
    #region Failure paths
    // =========================================================================

    [Fact]
    public async Task Signup_WhenTenantNotFound_ReturnsFailure()
    {
        _tenantRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((Domain.Identity.Tenant.Tenant?)null);

        var result = await CreateHandler().Handle(
            new SignupUserCommand("INVALID", "Alice", "alice@example.com", "Str0ng!Pass"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signup_WhenEmailAlreadyExists_ReturnsFailure()
    {
        var tenant   = MakeTenant();
        var existing = MakeActiveInternalAdmin();

        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(
            new SignupUserCommand(TenantCode, "Alice", "admin@acme.com", "Str0ng!Pass"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signup_WhenNoActiveTenantAdmin_ReturnsFailure()
    {
        var tenant = MakeTenant();

        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserAccount?)null);
        _userRepo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<UserAccount>());

        var result = await CreateHandler().Handle(
            new SignupUserCommand(TenantCode, "Alice", "alice@example.com", "Str0ng!Pass"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("admin", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signup_WhenOnlyExternalUsersInTenant_ReturnsFailure()
    {
        var tenant   = MakeTenant();
        var external = UserAccount.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(TenantId),
            Email.Create("ext@acme.com"),
            UserCategory.External,
            null, null,
            ActorId.Create("sys")).Value;
        external.Activate(ActorId.Create("sys"));

        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(tenant);
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((UserAccount?)null);
        _userRepo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<UserAccount> { external });

        var result = await CreateHandler().Handle(
            new SignupUserCommand(TenantCode, "Alice", "alice@example.com", "Str0ng!Pass"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("admin", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
