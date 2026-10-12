namespace Evolith.Ums.Application.Test.Authorization.Template;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Authorization.Template.Commands;
using Evolith.Ums.Application.Authorization.Template.DTOs;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Enums;
using Moq;
using Xunit;
using System;
using System.Threading;
using System.Threading.Tasks;

public class TemplateCommandHandlerTests
{
    private readonly Mock<IPermissionTemplateRepository> _repo = new();
    private readonly Mock<IProfileRepository>          _profiles = new();
    private readonly Mock<IUnitOfWork>                 _uow  = new();
    private readonly Mock<IUserContext>                _ctx  = new();
    private readonly Mock<ITenantScopePolicy>          _scopePolicy = new();

    public TemplateCommandHandlerTests()
    {
        _repo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _scopePolicy.Setup(s => s.EnsureManagementOwnerScopeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        // G-140: por defecto no hay plantillas previas para la terna → el alta usa la versión inicial.
        _repo.Setup(r => r.GetByTenantRoleSuiteAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PermissionTemplate>());
    }

    private static PermissionTemplate MakeTemplate()
    {
        var template = PermissionTemplate.Create(
            TenantId.Load(Guid.NewGuid()),
            RoleId.Load(Guid.NewGuid()),
            SystemSuiteId.Load(Guid.NewGuid()),
            ActorId.Create("user-001")).Value;

        var itemResult = template.AddItem(
            ExclusiveArcTarget.SystemSuite,
            IdValueObject.Create(),
            ActionId.Load(Guid.NewGuid()),
            isAllowed: true,
            isDenied: false,
            ActorId.Create("user-001"));

        if (itemResult.IsFailure)
        {
            throw new InvalidOperationException(itemResult.Error);
        }

        return template;
    }

    // =========================================================================
    #region CreatePermissionTemplateCommandHandler
    // =========================================================================

    [Fact]
    public async Task Create_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");

        var cmd = new CreatePermissionTemplateCommand(
            TenantId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            SystemSuiteId: Guid.NewGuid());

        var handler = new CreatePermissionTemplateCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.TemplateId);
        _repo.Verify(r => r.AddAsync(It.IsAny<PermissionTemplate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenNoExistingTemplate_AssignsInitialVersion()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        PermissionTemplate? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<PermissionTemplate>(), It.IsAny<CancellationToken>()))
             .Callback<PermissionTemplate, CancellationToken>((t, _) => captured = t)
             .Returns(Task.CompletedTask);

        var cmd = new CreatePermissionTemplateCommand(
            TenantId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            SystemSuiteId: Guid.NewGuid());

        var handler = new CreatePermissionTemplateCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal("0.1.0", captured!.Version.GetValue());
    }

    [Fact]
    public async Task Create_WhenRoleAlreadyTemplated_AssignsNextVersion()
    {
        // G-140: existe ya una plantilla v0.1.0 para la terna → el alta debe generar v0.2.0
        // en lugar de colisionar con el índice único.
        _ctx.Setup(u => u.UserId).Returns("user-001");

        var tenantId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();

        var existing = PermissionTemplate.Create(
            TenantId.Load(tenantId), RoleId.Load(roleId), SystemSuiteId.Load(suiteId),
            ActorId.Create("user-001")).Value;

        _repo.Setup(r => r.GetByTenantRoleSuiteAsync(tenantId, roleId, suiteId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new[] { existing });

        PermissionTemplate? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<PermissionTemplate>(), It.IsAny<CancellationToken>()))
             .Callback<PermissionTemplate, CancellationToken>((t, _) => captured = t)
             .Returns(Task.CompletedTask);

        var cmd = new CreatePermissionTemplateCommand(tenantId, roleId, suiteId);
        var handler = new CreatePermissionTemplateCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal("0.2.0", captured!.Version.GetValue());
    }

    [Fact]
    public async Task Create_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new CreatePermissionTemplateCommand(
            TenantId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            SystemSuiteId: Guid.NewGuid());

        var handler = new CreatePermissionTemplateCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
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

        var cmd = new CreatePermissionTemplateCommand(
            TenantId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            SystemSuiteId: Guid.NewGuid());

        var handler = new CreatePermissionTemplateCommandHandler(_repo.Object, _ctx.Object, _scopePolicy.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("management owner", result.Error, StringComparison.OrdinalIgnoreCase);
        _repo.Verify(r => r.AddAsync(It.IsAny<PermissionTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    // =========================================================================
    #region PublishPermissionTemplateCommandHandler
    // =========================================================================

    [Fact]
    public async Task Publish_WithValidCommand_ReturnsSuccess()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var template = MakeTemplate();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(template);

        var cmd = new PublishPermissionTemplateCommand(template.Props.Id.GetValue());
        var handler = new PublishPermissionTemplateCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateStatus.Published, template.Status);
        _repo.Verify(r => r.UpdateAsync(template, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publish_WhenNotFound_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((PermissionTemplate?)null);

        var cmd = new PublishPermissionTemplateCommand(Guid.NewGuid());
        var handler = new PublishPermissionTemplateCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("template was not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publish_WhenUnauthenticated_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("");

        var cmd = new PublishPermissionTemplateCommand(Guid.NewGuid());
        var handler = new PublishPermissionTemplateCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("authenticated user is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publish_WhenAlreadyPublished_ReturnsFailure()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var template = MakeTemplate();
        // Publish it first
        template.Publish(ActorId.Create("user-001"));

        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(template);

        var cmd = new PublishPermissionTemplateCommand(template.Props.Id.GetValue());
        var handler = new PublishPermissionTemplateCommandHandler(_repo.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    #endregion

    // =========================================================================
    #region DeletePermissionTemplateCommandHandler (borrado LÓGICO)
    // =========================================================================

    /// <summary>
    /// Política del propietario: solo existe borrado lógico. El handler ya NO llama a un DELETE
    /// físico —antes <c>DeleteAsync</c> hacía <c>Remove</c> y la fila desaparecía—; ahora transiciona
    /// el agregado al estado terminal y lo persiste con <c>UpdateAsync</c>.
    /// </summary>
    [Fact]
    public async Task Delete_ConPlantillaSinPerfilesVivos_MarcaEstadoTerminalYPersisteConUpdate()
    {
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var template = MakeTemplate(); // queda en Draft
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(template);
        _profiles.Setup(p => p.CountActiveByTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(0);

        var cmd = new DeletePermissionTemplateCommand(template.Props.Id.GetValue());
        var handler = new DeletePermissionTemplateCommandHandler(_repo.Object, _profiles.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TemplateStatus.Deleted, template.Status);
        _repo.Verify(r => r.UpdateAsync(template, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ConPerfilesVivos_DevuelveErrorDeOperacionBloqueada()
    {
        // Guardia de cascada → el error viaja codificado como BlockedOperationError, que la capa de
        // presentación traduce a 409 con las dependencias que bloquean.
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var template = MakeTemplate();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(template);
        _profiles.Setup(p => p.CountActiveByTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(3);

        var cmd = new DeletePermissionTemplateCommand(template.Props.Id.GetValue());
        var handler = new DeletePermissionTemplateCommandHandler(_repo.Object, _profiles.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.True(BlockedOperationError.TryDecode(result.Error, out var code, out var deps));
        Assert.Equal(DomainErrors.Authorization.TemplateHasActiveProfiles, code);
        Assert.Equal(3, deps.Single().Count);
        Assert.Equal("Profile", deps.Single().EntityType);
        // Nada se persiste y el agregado sigue vivo: el bloqueo es previo a cualquier escritura.
        Assert.NotEqual(TemplateStatus.Deleted, template.Status);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<PermissionTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_CuandoLaReferenciaYaNoEstaViva_SiPermiteEliminar()
    {
        // Contrapartida de la prueba anterior: los perfiles ya desactivados no cuentan como referencia
        // (CountActiveByTemplateAsync solo cuenta los activos), así que el borrado procede.
        _ctx.Setup(u => u.UserId).Returns("user-001");
        var template = MakeTemplate();
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(template);
        _profiles.SetupSequence(p => p.CountActiveByTemplateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(1)   // primer intento: perfil vivo
                 .ReturnsAsync(0);  // tras desactivarlo: ya no bloquea

        var cmd = new DeletePermissionTemplateCommand(template.Props.Id.GetValue());
        var handler = new DeletePermissionTemplateCommandHandler(_repo.Object, _profiles.Object, _ctx.Object);

        var bloqueado = await handler.Handle(cmd, CancellationToken.None);
        Assert.True(bloqueado.IsFailure);

        var permitido = await handler.Handle(cmd, CancellationToken.None);
        Assert.True(permitido.IsSuccess);
        Assert.Equal(TemplateStatus.Deleted, template.Status);
    }

    [Fact]
    public async Task Delete_CuandoYaEstaEliminada_DevuelveNoEncontrada()
    {
        // Las lecturas ocultan lo eliminado: el segundo DELETE no encuentra nada → 404, no un 500 ni
        // un 204 mentiroso. Se modela con el repositorio devolviendo null, que es lo que hace el store
        // real al filtrar por estado.
        _ctx.Setup(u => u.UserId).Returns("user-001");
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((PermissionTemplate?)null);

        var cmd = new DeletePermissionTemplateCommand(Guid.NewGuid());
        var handler = new DeletePermissionTemplateCommandHandler(_repo.Object, _profiles.Object, _ctx.Object);
        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
