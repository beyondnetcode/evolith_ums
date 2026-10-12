namespace Evolith.Ums.Application.Test.Configuration.IdpConfiguration;

using System;
using System.Threading;
using System.Threading.Tasks;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.IdpConfiguration;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

public class IdpConfigurationCommandHandlerTests
{
    private readonly Mock<IIdpConfigurationRepository> _repo = new();
    private readonly Mock<IUnitOfWork>                 _uow  = new();
    private readonly Mock<IUserContext>                _ctx  = new();

    public IdpConfigurationCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ctx.Setup(u => u.UserId).Returns("user-001");
    }

    private static IdpConfiguration MakeIdpConfiguration()
    {
        return IdpConfiguration.Create(
            TenantId.Load(Guid.NewGuid()),
            SystemSuiteId.Load(Guid.NewGuid()),
            ProviderType.Okta,
            new[] { "okta.com" },
            "{ \"payload\": true }",
            "secret-123",
            10,
            null,
            ActorId.Create("user-001")).Value;
    }

    // =========================================================================
    #region CreateIdpConfigurationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");

        var cmd = new CreateIdpConfigurationCommand(
            TenantId: Guid.NewGuid(),
            SystemSuiteId: Guid.NewGuid(),
            ProviderType: "OKTA",
            DomainHints: new[] { "okta.com" },
            ConfigPayload: "{ \"payload\": true }",
            SecretRef: "secret-123",
            ResolutionPriority: 10,
            FallbackToId: null);

        var handler = new CreateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.IdpConfigurationId);
        _repo.Verify(r => r.AddAsync(It.IsAny<IdpConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenInvalidProviderType_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");

        var cmd = new CreateIdpConfigurationCommand(
            TenantId: Guid.NewGuid(),
            SystemSuiteId: Guid.NewGuid(),
            ProviderType: "INVALID_PROVIDER",
            DomainHints: new[] { "okta.com" },
            ConfigPayload: "{ \"payload\": true }",
            SecretRef: "secret-123",
            ResolutionPriority: 10,
            FallbackToId: null);

        var handler = new CreateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("invalid identity provider type", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreateIdpConfigurationCommand(
            TenantId: Guid.NewGuid(),
            SystemSuiteId: Guid.NewGuid(),
            ProviderType: "OKTA",
            DomainHints: new[] { "okta.com" },
            ConfigPayload: "{ \"payload\": true }",
            SecretRef: "secret-123",
            ResolutionPriority: 10,
            FallbackToId: null);

        var handler = new CreateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region ActivateIdpConfigurationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Activate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakeIdpConfiguration(); // Born Draft/Inactive status
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var cmd = new ActivateIdpConfigurationCommand(Guid.NewGuid());
        var handler = new ActivateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IdpConfigStatus.Active, config.Status);
        _repo.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Activate_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((IdpConfiguration?)null);

        var cmd = new ActivateIdpConfigurationCommand(Guid.NewGuid());
        var handler = new ActivateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region DeactivateIdpConfigurationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Deactivate_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakeIdpConfiguration();
        config.Activate(ActorId.Create("user-001")); // Make Active first

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var cmd = new DeactivateIdpConfigurationCommand(Guid.NewGuid());
        var handler = new DeactivateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IdpConfigStatus.Inactive, config.Status);
        _repo.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deactivate_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((IdpConfiguration?)null);

        var cmd = new DeactivateIdpConfigurationCommand(Guid.NewGuid());
        var handler = new DeactivateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region UpdateIdpConfigurationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Update_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakeIdpConfiguration(); // Born Draft/Inactive, ready for update

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var cmd = new UpdateIdpConfigurationCommand(
            IdpConfigurationId: Guid.NewGuid(),
            ConfigPayload: "{ \"new\": \"payload\" }",
            SecretRef: "new-secret",
            DomainHints: new[] { "new.okta.com" });

        var handler = new UpdateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((IdpConfiguration?)null);

        var cmd = new UpdateIdpConfigurationCommand(
            IdpConfigurationId: Guid.NewGuid(),
            ConfigPayload: "{ \"new\": \"payload\" }",
            SecretRef: "new-secret",
            DomainHints: new[] { "new.okta.com" });

        var handler = new UpdateIdpConfigurationCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion
}
