namespace Evolith.Ums.Application.Test.Identity.UserAccount;

using System;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Application.Identity.UserAccount.Commands;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Moq;
using Xunit;

public class UserAccountValidityCommandHandlerTests
{
    private readonly Mock<IUserAccountRepository> _repo = new();
    private readonly Mock<IUnitOfWork>            _uow  = new();
    private readonly Mock<IUserContext>            _ctx  = new();
    private readonly Mock<ITenantContext>          _tenant = new();
    private readonly Mock<IConfigurationProvider>  _config = new();

    public UserAccountValidityCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("admin-001");
        _tenant.Setup(t => t.IsInternalAdmin).Returns(true);
        _config.Setup(c => c.GetValueAs(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<int?>()))
            .Returns(365);
    }

    private static UserAccount MakeUserAccount(Guid? tenantId = null)
    {
        return UserAccount.Create(
            TenantId.Load(tenantId ?? Guid.NewGuid()),
            Email.Create("user@test.com"),
            UserCategory.Internal,
            null, null,
            ActorId.Create("admin-001")).Value;
    }

    [Fact]
    public async Task ModifyValidityPeriod_WithFutureDate_ReturnsSuccess()
    {
        var user = MakeUserAccount();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new ModifyUserValidityPeriodCommand(user.GetId().GetValue(), DateTimeOffset.UtcNow.AddDays(30));
        var handler = new ModifyUserValidityPeriodCommandHandler(
            _repo.Object, _ctx.Object, _tenant.Object, _config.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(user.ExpiresAt);
        _repo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ModifyValidityPeriod_WhenNotFound_ReturnsFailure()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((UserAccount?)null);

        var cmd = new ModifyUserValidityPeriodCommand(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(30));
        var handler = new ModifyUserValidityPeriodCommandHandler(
            _repo.Object, _ctx.Object, _tenant.Object, _config.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModifyValidityPeriod_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new ModifyUserValidityPeriodCommand(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(30));
        var handler = new ModifyUserValidityPeriodCommandHandler(
            _repo.Object, _ctx.Object, _tenant.Object, _config.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ModifyValidityPeriod_WhenExceedsMaxDays_ReturnsFailure()
    {
        var user = MakeUserAccount();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);

        var cmd = new ModifyUserValidityPeriodCommand(user.GetId().GetValue(), DateTimeOffset.UtcNow.AddDays(400));
        var handler = new ModifyUserValidityPeriodCommandHandler(
            _repo.Object, _ctx.Object, _tenant.Object, _config.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ModifyValidityPeriod_WhenCrossTenantByTenantAdmin_ReturnsFailure()
    {
        var userTenantId = Guid.NewGuid();
        var adminTenantId = Guid.NewGuid();
        var user = MakeUserAccount(userTenantId);

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);
        _tenant.Setup(t => t.IsInternalAdmin).Returns(false);
        _tenant.Setup(t => t.OrganizationId).Returns(adminTenantId);

        var cmd = new ModifyUserValidityPeriodCommand(user.GetId().GetValue(), DateTimeOffset.UtcNow.AddDays(30));
        var handler = new ModifyUserValidityPeriodCommandHandler(
            _repo.Object, _ctx.Object, _tenant.Object, _config.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("AUTH_010", result.Error);
    }

    [Fact]
    public async Task ModifyValidityPeriod_WhenTenantAdminInScope_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        var user = MakeUserAccount(tenantId);

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);
        _tenant.Setup(t => t.IsInternalAdmin).Returns(false);
        _tenant.Setup(t => t.OrganizationId).Returns(tenantId);

        var cmd = new ModifyUserValidityPeriodCommand(user.GetId().GetValue(), DateTimeOffset.UtcNow.AddDays(30));
        var handler = new ModifyUserValidityPeriodCommandHandler(
            _repo.Object, _ctx.Object, _tenant.Object, _config.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }
}
