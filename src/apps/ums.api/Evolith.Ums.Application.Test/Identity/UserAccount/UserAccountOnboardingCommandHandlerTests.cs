namespace Evolith.Ums.Application.Test.Identity.UserAccount;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Application.Identity.UserAccount.Commands;
using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Application.Identity.UserAccount.Queries;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Enums;
using Moq;
using Xunit;

/// <summary>
/// Unit tests for EP-09 onboarding handlers:
///   - DenyUserSignupCommandHandler
///   - GetPendingUserSignupRequestsQueryHandler
/// </summary>
public class UserAccountOnboardingCommandHandlerTests
{
    private readonly Mock<IUserAccountRepository> _repo           = new();
    private readonly Mock<IUnitOfWork>            _uow            = new();
    private readonly Mock<IUserContext>            _ctx            = new();
    private readonly Mock<ITenantRepository>       _tenantRepo     = new();
    private readonly Mock<INotificationService>    _notifications  = new();
    private readonly Mock<ITenantContext>          _tenantCtx      = new();

    private static readonly Guid TenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    public UserAccountOnboardingCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("admin-001");
        _tenantCtx.Setup(t => t.OrganizationId).Returns(TenantId);

        var tenant = Evolith.Ums.Domain.Identity.Tenant.Tenant.Create(
            Code.Create("TEST"), Name.Create("Test Tenant"),
            OrganizationType.INTERNAL, ActorId.Create("sys"),
            tenantId: Domain.Kernel.ValueObjects.TenantId.Load(TenantId)).Value;
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
    }

    private static UserAccount MakePendingUser()
    {
        return UserAccount.Create(
            Domain.Kernel.ValueObjects.TenantId.Load(TenantId),
            Email.Create("pending@test.com"),
            UserCategory.External,
            null, null,
            ActorId.Create("sys")).Value;
    }

    // =========================================================================
    #region DenyUserSignupCommandHandler
    // =========================================================================

    [Fact]
    public async Task Deny_WithValidPendingUser_ReturnsSuccess()
    {
        var user = MakePendingUser();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var handler = new DenyUserSignupCommandHandler(_repo.Object, _tenantRepo.Object, _ctx.Object, _notifications.Object);
        var result  = await handler.Handle(new DenyUserSignupCommand(Guid.NewGuid(), "Not eligible"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Denied, user.Status);
        _repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deny_SendsNotificationToApplicant()
    {
        var user = MakePendingUser();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var handler = new DenyUserSignupCommandHandler(_repo.Object, _tenantRepo.Object, _ctx.Object, _notifications.Object);
        await handler.Handle(new DenyUserSignupCommand(Guid.NewGuid(), "Not eligible"), CancellationToken.None);

        _notifications.Verify(n => n.SendAsync(It.IsAny<UmsNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deny_WhenUserNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserAccount?)null);

        var handler = new DenyUserSignupCommandHandler(_repo.Object, _tenantRepo.Object, _ctx.Object, _notifications.Object);
        var result  = await handler.Handle(new DenyUserSignupCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deny_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new DenyUserSignupCommandHandler(_repo.Object, _tenantRepo.Object, _ctx.Object, _notifications.Object);
        var result  = await handler.Handle(new DenyUserSignupCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deny_ActiveUser_ReturnsFailure()
    {
        var user = MakePendingUser();
        user.Activate(ActorId.Create("admin"));
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var handler = new DenyUserSignupCommandHandler(_repo.Object, _tenantRepo.Object, _ctx.Object, _notifications.Object);
        var result  = await handler.Handle(new DenyUserSignupCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.UserAccount.CannotDeny, result.Error);
    }

    #endregion

    // =========================================================================
    #region GetPendingUserSignupRequestsQueryHandler
    // =========================================================================

    [Fact]
    public async Task GetPendingUserSignups_ReturnsPendingUsersOnly()
    {
        var pending = MakePendingUser();
        var active  = MakePendingUser();
        active.Activate(ActorId.Create("admin"));

        _repo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UserAccount> { pending, active });

        var handler = new GetPendingUserSignupRequestsQueryHandler(_repo.Object, _tenantCtx.Object);
        var result  = await handler.Handle(new GetPendingUserSignupRequestsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("pending@test.com", result.Value[0].Email);
    }

    [Fact]
    public async Task GetPendingUserSignups_WhenNoTenantContext_ReturnsFailure()
    {
        _tenantCtx.Setup(t => t.OrganizationId).Returns((Guid?)null);

        var handler = new GetPendingUserSignupRequestsQueryHandler(_repo.Object, _tenantCtx.Object);
        var result  = await handler.Handle(new GetPendingUserSignupRequestsQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Tenant context is required", result.Error);
    }

    [Fact]
    public async Task GetPendingUserSignups_OrderedByRequestedAt()
    {
        var user1 = MakePendingUser();
        var user2 = MakePendingUser();

        _repo.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UserAccount> { user1, user2 });

        var handler = new GetPendingUserSignupRequestsQueryHandler(_repo.Object, _tenantCtx.Object);
        var result  = await handler.Handle(new GetPendingUserSignupRequestsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    #endregion
}
