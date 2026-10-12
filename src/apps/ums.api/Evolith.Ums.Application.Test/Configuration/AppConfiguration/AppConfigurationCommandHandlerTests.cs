namespace Evolith.Ums.Application.Test.Configuration.AppConfiguration;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.AppConfiguration.Commands;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;
using Xunit;

using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

/// <summary>
/// Application-layer tests for AppConfiguration command handlers.
///
/// Coverage intent:
///   – Auth guard: unauthenticated callers are rejected before touching the domain.
///   – Duplicate scope+code guard in CreateAppConfigurationCommandHandler.
///   – Domain failure (illegal state transitions) surfaced through lifecycle handlers.
///   – Not-found guard in all handlers that load by Id.
///   – Repository interactions (AddAsync / UpdateAsync / SaveEntities) called exactly once on success.
///   – Global scope path (all nullable Ids omitted) validated through create handler.
///
/// Excluded intentionally:
///   – Query handlers (read-only projections — no business logic).
///   – FluentValidation pipeline (tested via ValidationBehaviorTests).
///   – Scope resolution itself (covered by domain tests in AppConfigurationTests).
/// </summary>
public class AppConfigurationCommandHandlerTests
{
    // -------------------------------------------------------------------------
    // Shared mocks & helpers
    // -------------------------------------------------------------------------

    private readonly Mock<IAppConfigurationRepository> _repo           = new();
    private readonly Mock<IUnitOfWork>                 _uow            = new();
    private readonly Mock<IUserContext>                _ctx            = new();
    private readonly Mock<IConfigurationProvider>      _configProvider = new();
    private readonly Mock<IValueEncryptionService>     _encryption     = new();

    public AppConfigurationCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _configProvider.Setup(p => p.ReloadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        // No-op encryption for most tests — specific tests set up their own behaviour.
        _encryption.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(v => v);
        _encryption.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(v => v);
        _encryption.Setup(e => e.IsEncryptedValue(It.IsAny<string>())).Returns(false);
        _configProvider.Setup(p => p.ReloadTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private static CreateAppConfigurationCommand ValidCreateCmd => new(
        TenantId: Guid.NewGuid(),
        SystemSuiteId: Guid.NewGuid(),
        ModuleId: null,
        Code: "CFG-001",
        Value: "value123",
        Description: "Test configuration",
        IsInheritable: true,
        IsEncrypted: false);

    private static AppConfigurationAggregate MakeDraft() =>
        AppConfigurationAggregate.Create(
            TenantId.Load(Guid.NewGuid()),
            SystemSuiteId.Load(Guid.NewGuid()),
            null,
            Code.Create("CFG-001"),
            ConfigurationValue.Create("value123"),
            Description.Create("Test configuration"),
            true, false,
            ActorId.Create("user-001")).Value;

    private static AppConfigurationAggregate MakePublished()
    {
        var c = MakeDraft();
        c.Publish(ActorId.Create("user-001"));
        return c;
    }

    private static AppConfigurationAggregate MakeArchived()
    {
        var c = MakePublished();
        c.Archive(ActorId.Create("user-001"));
        return c;
    }

    // =========================================================================
    #region CreateAppConfigurationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByScopeAndCodeAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.AppConfigurationId);
    }

    [Fact]
    public async Task Create_SavesAggregateToRepository()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByScopeAndCodeAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        await handler.Handle(ValidCreateCmd, CancellationToken.None);

        _repo.Verify(r => r.AddAsync(It.IsAny<AppConfigurationAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithGlobalScope_ReturnsSuccess()
    {
        // All scope IDs null → ConfigurationScope.Global
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByScopeAndCodeAsync(null, null, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);
        var cmd = ValidCreateCmd with { TenantId = null, SystemSuiteId = null, ModuleId = null };

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_WhenCodeAlreadyExistsForScope_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByScopeAndCodeAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeDraft()); // duplicate found

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    [Fact]
    public async Task Create_WhenUserIdNull_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns((string?)null);

        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(ValidCreateCmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region PublishAppConfigurationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Publish_WhenDraft_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakeDraft();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var handler = new PublishAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new PublishAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var handler = new PublishAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new PublishAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task Publish_WhenAlreadyPublished_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakePublished());

        var handler = new PublishAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new PublishAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNotDraft, result.Error);
    }

    [Fact]
    public async Task Publish_WhenArchived_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeArchived());

        var handler = new PublishAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new PublishAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNotDraft, result.Error);
    }

    [Fact]
    public async Task Publish_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new PublishAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new PublishAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    #endregion

    // =========================================================================
    #region ArchiveAppConfigurationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Archive_WhenPublished_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakePublished();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var handler = new ArchiveAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new ArchiveAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Archive_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var handler = new ArchiveAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new ArchiveAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task Archive_WhenDraft_ReturnsFailure()
    {
        // Draft → Archive is forbidden; must go Draft → Published → Archived
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeDraft());

        var handler = new ArchiveAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new ArchiveAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNotPublished, result.Error);
    }

    [Fact]
    public async Task Archive_WhenAlreadyArchived_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeArchived());

        var handler = new ArchiveAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new ArchiveAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNotPublished, result.Error);
    }

    [Fact]
    public async Task Archive_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new ArchiveAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new ArchiveAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    #endregion

    // =========================================================================
    #region DeleteAppConfigurationCommandHandler (borrado LÓGICO)
    // =========================================================================

    [Fact]
    public async Task Delete_WhenExists_MarcaEliminadaYNoRetiraLaFila()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakePublished();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var handler = new DeleteAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new DeleteAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // La política del propietario, expresada como aserción: el agregado pasa al estado terminal
        // y se persiste con UpdateAsync. El repositorio no expone ninguna baja física.
        Assert.Equal(ConfigStatus.Deleted, config.Status);
        _repo.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_DesalojaLaCache()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakePublished();   // configuración de inquilino → recarga acotada al inquilino
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var handler = new DeleteAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new DeleteAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Si la configuración deja de resolver, la caché tiene que enterarse igual: la fila sobrevive
        // pero su valor ya no participa en la resolución.
        _configProvider.Verify(
            p => p.ReloadTenantAsync(config.Props.TenantId!.GetValue(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var handler = new DeleteAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new DeleteAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<AppConfigurationAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SobreYaEliminada_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakePublished();
        config.Delete(ActorId.Create("user-000"));   // ya eliminada por otro actor
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var handler = new DeleteAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new DeleteAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigAlreadyDeleted, result.Error);
    }

    [Fact]
    public async Task Delete_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new DeleteAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object);
        var result  = await handler.Handle(new DeleteAppConfigurationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    #endregion

    // =========================================================================
    #region UpdateAppConfigurationCommandHandler
    // =========================================================================

    [Fact]
    public async Task Update_WhenDraft_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var config = MakeDraft();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(config);

        var handler = new UpdateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object, _encryption.Object);
        var result  = await handler.Handle(
            new UpdateAppConfigurationCommand(Guid.NewGuid(), "newvalue", "Updated description"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.UpdateAsync(config, It.IsAny<byte[]?>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var handler = new UpdateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object, _encryption.Object);
        var result  = await handler.Handle(
            new UpdateAppConfigurationCommand(Guid.NewGuid(), "v", "d"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task Update_WhenPublished_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakePublished());

        var handler = new UpdateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object, _encryption.Object);
        var result  = await handler.Handle(
            new UpdateAppConfigurationCommand(Guid.NewGuid(), "v", "d"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNotDraft, result.Error);
    }

    [Fact]
    public async Task Update_WhenArchived_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeArchived());

        var handler = new UpdateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object, _encryption.Object);
        var result  = await handler.Handle(
            new UpdateAppConfigurationCommand(Guid.NewGuid(), "v", "d"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNotDraft, result.Error);
    }

    [Fact]
    public async Task Update_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var handler = new UpdateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _configProvider.Object, _encryption.Object);
        var result  = await handler.Handle(
            new UpdateAppConfigurationCommand(Guid.NewGuid(), "v", "d"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Authenticated user is required", result.Error);
    }

    #endregion

    // =========================================================================
    #region IsNonOverridable guard (BR-2)
    // =========================================================================

    private static AppConfigurationAggregate MakeNonOverridableGlobal() =>
        AppConfigurationAggregate.Create(
            null, null, null,
            Code.Create("CFG-001"),
            ConfigurationValue.Create("global-value"),
            Description.Create("Global non-overridable config"),
            true, false,
            ActorId.Create("admin"),
            isNonOverridable: true).Value;

    [Fact]
    public async Task Create_TenantScope_WhenGlobalIsNonOverridable_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        _ctx.Setup(u => u.UserId).Returns("user-001");

        // No duplicate at tenant scope
        _repo.Setup(r => r.GetByScopeAndCodeAsync(tenantId, null, null, "CFG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);
        // Global scope has IsNonOverridable = true
        _repo.Setup(r => r.GetByScopeAndCodeAsync(null, null, null, "CFG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeNonOverridableGlobal());

        var cmd     = ValidCreateCmd with { TenantId = tenantId, SystemSuiteId = null, ModuleId = null };
        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNonOverridable, result.Error);
    }

    [Fact]
    public async Task Create_SuiteScope_WhenTenantIsNonOverridable_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        var suiteId  = Guid.NewGuid();
        _ctx.Setup(u => u.UserId).Returns("user-001");

        // No duplicate at suite scope
        _repo.Setup(r => r.GetByScopeAndCodeAsync(tenantId, suiteId, null, "CFG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);
        // Tenant scope has IsNonOverridable = true
        _repo.Setup(r => r.GetByScopeAndCodeAsync(tenantId, null, null, "CFG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeNonOverridableGlobal());
        // Global scope is fine
        _repo.Setup(r => r.GetByScopeAndCodeAsync(null, null, null, "CFG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var cmd     = ValidCreateCmd with { TenantId = tenantId, SystemSuiteId = suiteId, ModuleId = null };
        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(DomainErrors.Configuration.AppConfigNonOverridable, result.Error);
    }

    [Fact]
    public async Task Create_TenantScope_WhenGlobalIsOverridable_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        _ctx.Setup(u => u.UserId).Returns("user-001");

        _repo.Setup(r => r.GetByScopeAndCodeAsync(tenantId, null, null, "CFG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);
        // Global scope exists but IsNonOverridable = false (default)
        _repo.Setup(r => r.GetByScopeAndCodeAsync(null, null, null, "CFG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync(MakeDraft()); // IsNonOverridable defaults to false

        var cmd     = ValidCreateCmd with { TenantId = tenantId, SystemSuiteId = null, ModuleId = null };
        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_GlobalScope_NeverChecksParents_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByScopeAndCodeAsync(null, null, null, "CFG-001", It.IsAny<CancellationToken>()))
             .ReturnsAsync((AppConfigurationAggregate?)null);

        var cmd     = ValidCreateCmd with { TenantId = null, SystemSuiteId = null, ModuleId = null };
        var handler = new CreateAppConfigurationCommandHandler(_repo.Object, _ctx.Object, _encryption.Object);
        var result  = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // GetByScopeAndCodeAsync called exactly once for the duplicate check, never for a parent check
        _repo.Verify(r => r.GetByScopeAndCodeAsync(
            It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
