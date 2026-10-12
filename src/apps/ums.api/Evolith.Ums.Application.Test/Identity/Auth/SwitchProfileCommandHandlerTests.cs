namespace Evolith.Ums.Application.Test.Identity.Auth;

using Moq;
using Xunit;
using FluentAssertions;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Auth.Commands;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Kernel;
using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

/// <summary>
/// Reglas del cambio de perfil. Las dos primeras son el motivo por el que este endpoint no es
/// trivial: el identificador del perfil lo envía el cliente, así que si no se comprueba que
/// pertenece al usuario del token y a su inquilino, cambiar de perfil se convierte en una escalada
/// de privilegios de una línea.
/// </summary>
public class SwitchProfileCommandHandlerTests
{
    private readonly Mock<IUserAccountRepository>     _userRepo     = new();
    private readonly Mock<IProfileRepository>         _profileRepo  = new();
    private readonly Mock<IAuthorizationGraphBuilder> _graphBuilder = new();
    private readonly Mock<IAuthAuditService>          _audit        = new();

    private static readonly Guid TenantGuid  = Guid.NewGuid();
    private static readonly Guid UserGuid    = Guid.NewGuid();
    private static readonly Guid ProfileGuid = Guid.NewGuid();

    private SwitchProfileCommandHandler CreateSut()
        => new(_userRepo.Object, _profileRepo.Object, _graphBuilder.Object, _audit.Object);

    private static SwitchProfileCommand Command()
        => new(ProfileGuid, UserGuid, TenantGuid, "10.0.0.1");

    private static UserAccountAggregate UsuarioActivo()
    {
        var user = UserAccountAggregate.Create(
            TenantId.Load(TenantGuid),
            Email.Create("ana@evolith.example"),
            UserCategory.Internal,
            null, null,
            ActorId.Create("system"),
            null,
            UserAccountId.Load(UserGuid)).Value;

        user.Activate(ActorId.Create("system"));
        return user;
    }

    private static ProfileAggregate Perfil(Guid userId, Guid tenantId, bool activo = true)
    {
        var profile = ProfileAggregate.Create(
            TenantId.Load(tenantId),
            UserId.Load(userId),
            RoleId.Load(Guid.NewGuid()),
            null,
            ActorId.Create("system")).Value;

        if (!activo) profile.Deactivate(ActorId.Create("system"));
        return profile;
    }

    private void ConUsuarioYPerfil(ProfileAggregate perfil)
    {
        _userRepo.Setup(r => r.GetByIdAsync(UserGuid, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(UsuarioActivo());
        _profileRepo.Setup(r => r.GetByIdAsync(ProfileGuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(perfil);
    }

    [Fact]
    public async Task Un_perfil_de_otro_usuario_no_se_puede_asumir()
    {
        ConUsuarioYPerfil(Perfil(userId: Guid.NewGuid(), tenantId: TenantGuid));

        var resultado = await CreateSut().Handle(Command(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        // Mismo mensaje que «no existe»: distinguirlos permitiría enumerar perfiles ajenos.
        resultado.Error.Should().Contain("AUTH_020");
        _graphBuilder.Verify(b => b.BuildForProfileAsync(
            It.IsAny<UserAccountAggregate>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<AuthMethod>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_perfil_de_otro_inquilino_no_se_puede_asumir()
    {
        ConUsuarioYPerfil(Perfil(userId: UserGuid, tenantId: Guid.NewGuid()));

        var resultado = await CreateSut().Handle(Command(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("AUTH_020");
    }

    [Fact]
    public async Task El_intento_denegado_queda_auditado()
    {
        ConUsuarioYPerfil(Perfil(userId: Guid.NewGuid(), tenantId: TenantGuid));

        await CreateSut().Handle(Command(), CancellationToken.None);

        _audit.Verify(a => a.RecordAuthEventAsync(
            It.Is<AuthAuditEvent>(e => e.EventType == "Auth.Profile.SwitchDenied" && !e.Succeeded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Un_perfil_inactivo_no_se_puede_asumir()
    {
        ConUsuarioYPerfil(Perfil(UserGuid, TenantGuid, activo: false));

        var resultado = await CreateSut().Handle(Command(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("AUTH_021");
    }

    [Fact]
    public async Task Un_perfil_inexistente_devuelve_no_encontrado()
    {
        _userRepo.Setup(r => r.GetByIdAsync(UserGuid, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(UsuarioActivo());
        _profileRepo.Setup(r => r.GetByIdAsync(ProfileGuid, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((ProfileAggregate?)null);

        var resultado = await CreateSut().Handle(Command(), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Contain("AUTH_020");
    }
}
