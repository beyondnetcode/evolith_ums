namespace Evolith.Ums.Application.Test.Tenants.IdentityProvider;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Tenant.IdentityProvider.Commands;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Moq;
using Xunit;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class IdentityProviderCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _repo = new();
    private readonly Mock<IUnitOfWork>       _uow  = new();
    private readonly Mock<IUserContext>      _ctx  = new();
    private readonly Mock<ITenantScopePolicy> _scopePolicy = new();

    public IdentityProviderCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private static Tenant MakeTenant()
    {
        return Tenant.Create(
            Code.Create("TENANT-001"),
            Name.Create("Test Tenant"),
            OrganizationType.INTERNAL,
            ActorId.Create("user-001")).Value;
    }

    // =========================================================================
    #region RegisterIdentityProviderCommandHandler
    // =========================================================================

    [Fact]
    public async Task Register_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var tenant = MakeTenant();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new RegisterIdentityProviderCommand(
            TenantId: tenant.Props.Id.GetValue(),
            Code: "IDP-001",
            Name: "Azure AD",
            Description: "Corporate Identity Provider",
            Strategy: "AzureAd");

        var handler = new RegisterIdentityProviderCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(tenant.IdentityProviders);
        _repo.Verify(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_WhenTenantNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Tenant?)null);

        var cmd = new RegisterIdentityProviderCommand(
            TenantId: Guid.NewGuid(),
            Code: "IDP-001",
            Name: "Azure AD",
            Description: "Corporate Identity Provider",
            Strategy: "AzureAd");

        var handler = new RegisterIdentityProviderCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("tenant was not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WhenInvalidStrategy_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var tenant = MakeTenant();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new RegisterIdentityProviderCommand(
            TenantId: tenant.Props.Id.GetValue(),
            Code: "IDP-001",
            Name: "Azure AD",
            Description: "Corporate Identity Provider",
            Strategy: "INVALID_STRATEGY");

        var handler = new RegisterIdentityProviderCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("invalid identity provider strategy", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WhenTenantIsNotManagementOwner_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var tenant = MakeTenant();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(tenant.Props.Id.GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("AUTH_015: Tenant is not marked as management owner."));

        var cmd = new RegisterIdentityProviderCommand(
            TenantId: tenant.Props.Id.GetValue(),
            Code: "IDP-001",
            Name: "Azure AD",
            Description: "Corporate Identity Provider",
            Strategy: "AzureAd");

        var handler = new RegisterIdentityProviderCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("management owner", result.Error, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    // =========================================================================
    #region ActivateIdentityProviderCommandHandler
    // =========================================================================

    [Fact]
    public async Task Activate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var tenant = MakeTenant();
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure"), Description.Create("Azure"), IdpStrategy.AzureAd, ActorId.Create("user-001"));
        var idp = tenant.IdentityProviders.First();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new ActivateIdentityProviderCommand(tenant.Props.Id.GetValue(), idp.GetId().GetValue());
        var handler = new ActivateIdentityProviderCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(idp.IsActive);
        _repo.Verify(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    // =========================================================================
    #region DeactivateIdentityProviderCommandHandler
    // =========================================================================

    [Fact]
    public async Task Deactivate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var tenant = MakeTenant();
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure"), Description.Create("Azure"), IdpStrategy.AzureAd, ActorId.Create("user-001"));
        var idp = tenant.IdentityProviders.First();
        tenant.ActivateIdentityProvider(idp.GetId(), ActorId.Create("user-001")); // make active

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new DeactivateIdentityProviderCommand(tenant.Props.Id.GetValue(), idp.GetId().GetValue());
        var handler = new DeactivateIdentityProviderCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.False(idp.IsActive);
        _repo.Verify(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    // =========================================================================
    #region RemoveIdentityProviderCommandHandler
    // =========================================================================

    [Fact]
    public async Task Remove_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var tenant = MakeTenant();
        tenant.RegisterIdentityProvider(Code.Create("IDP-001"), Name.Create("Azure"), Description.Create("Azure"), IdpStrategy.AzureAd, ActorId.Create("user-001"));
        var idp = tenant.IdentityProviders.First();

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(tenant);

        var cmd = new RemoveIdentityProviderCommand(tenant.Props.Id.GetValue(), idp.GetId().GetValue());
        var handler = new RemoveIdentityProviderCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Empty(tenant.IdentityProviders);
        _repo.Verify(r => r.UpdateAsync(tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
