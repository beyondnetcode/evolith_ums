namespace Evolith.Ums.Application.Test.Identity.Auth;

using System.Text.Json;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Application.Identity.Auth.Commands;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Kernel;
using Moq;
using Xunit;

/// <summary>
/// G-188: la solicitud anónima de restablecimiento no puede filtrar la contraseña, no puede
/// cambiarla y no puede delatar si la cuenta existe. Estas pruebas fijan las tres cosas.
/// </summary>
public class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _tenantRepo = new();
    private readonly Mock<IUserAccountRepository> _userRepo = new();
    private readonly Mock<IPasswordResetTokenStore> _resetTokens = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IResponseTimingNormalizer> _timing = new();
    private readonly Mock<INotificationLinkBuilder> _enlaces = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid TenantIdValue = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid OtherTenantIdValue = Guid.Parse("9c1c2b3a-0000-4000-8000-000000000099");
    private const string TenantCode = "ACME";
    private const string UserEmail = "admin@acme.com";
    private const string ExistingHash = "hash-de-la-clave-vigente";

    public ForgotPasswordCommandHandlerTests()
    {
        _userRepo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static Domain.Identity.Tenant.Tenant MakeTenant() =>
        Domain.Identity.Tenant.Tenant.Create(
            Code.Create(TenantCode), Name.Create("Acme Corp"),
            OrganizationType.INTERNAL, ActorId.Create("sys"),
            tenantId: TenantId.Load(TenantIdValue)).Value;

    private static UserAccount MakeUserWithPassword(Guid tenantId)
    {
        var user = UserAccount.Create(
            TenantId.Load(tenantId),
            Email.Create(UserEmail),
            UserCategory.Internal,
            null, null,
            ActorId.Create("sys")).Value;
        user.Activate(ActorId.Create("sys"));
        user.AddPassword(PasswordHash.Create(ExistingHash), ActorId.Create("sys"));
        return user;
    }

    private ForgotPasswordCommandHandler CreateHandler() =>
        new(_tenantRepo.Object, _userRepo.Object, _resetTokens.Object, _notifications.Object,
            _timing.Object, _enlaces.Object);

    private void GivenTenantExists() =>
        _tenantRepo.Setup(r => r.GetByCodeAsync(TenantCode, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MakeTenant());

    private void GivenAccount(UserAccount? account) =>
        _userRepo.Setup(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(account);

    private Task<Result<ForgotPasswordResponse>> Invoke(string email = UserEmail) =>
        CreateHandler().Handle(new ForgotPasswordCommand(TenantCode, email), CancellationToken.None);

    // =========================================================================
    #region No se filtra la contraseña
    // =========================================================================

    [Fact]
    public void ForgotPassword_ResponseCarriesOnlyTheAmbiguousMessage()
    {
        // El contrato mismo es la defensa: si alguien reintroduce un campo con la credencial,
        // esta prueba se pone en rojo antes de que llegue a un entorno.
        var propiedades = typeof(ForgotPasswordResponse).GetProperties().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "Message" }, propiedades);
    }

    [Fact]
    public async Task ForgotPassword_WhenAccountExists_ResponseNeverContainsTheIssuedToken()
    {
        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(TenantIdValue));

        string? hashEmitido = null;
        _resetTokens
            .Setup(s => s.IssueAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, DateTime, DateTime, CancellationToken>(
                (_, _, hash, _, _, _) => hashEmitido = hash);

        string? tokenNotificado = null;
        _notifications
            .Setup(n => n.SendAsync(It.IsAny<UmsNotification>(), It.IsAny<CancellationToken>()))
            .Callback<UmsNotification, CancellationToken>((n, _) => tokenNotificado = n.Body);

        var result = await Invoke();
        var cuerpo = JsonSerializer.Serialize(result.Value);

        Assert.NotNull(hashEmitido);
        Assert.NotNull(tokenNotificado);
        Assert.DoesNotContain(hashEmitido!, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(ExistingHash, cuerpo, StringComparison.Ordinal);
    }

    #endregion

    // =========================================================================
    #region La respuesta es indistinguible
    // =========================================================================

    [Fact]
    public async Task ForgotPassword_ResponseIsByteIdenticalForExistingAndUnknownAccount()
    {
        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(TenantIdValue));
        var conCuenta = await Invoke();

        GivenAccount(null);
        var sinCuenta = await Invoke("nadie@acme.com");

        Assert.True(conCuenta.IsSuccess);
        Assert.True(sinCuenta.IsSuccess);
        Assert.Equal(
            JsonSerializer.Serialize(conCuenta.Value),
            JsonSerializer.Serialize(sinCuenta.Value));
    }

    [Fact]
    public async Task ForgotPassword_WhenTenantIsUnknown_ResponseIsTheSame()
    {
        _tenantRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((Domain.Identity.Tenant.Tenant?)null);

        var sinTenant = await Invoke();

        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(TenantIdValue));
        var conTodo = await Invoke();

        Assert.Equal(
            JsonSerializer.Serialize(conTodo.Value),
            JsonSerializer.Serialize(sinTenant.Value));
    }

    [Fact]
    public async Task ForgotPassword_AlwaysNormalizesResponseTiming()
    {
        // Sin nivelar el reloj, el camino «existe» (lectura + escritura + notificación) tarda
        // sistemáticamente más que el camino «no existe»: el tiempo sería el oráculo.
        _tenantRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((Domain.Identity.Tenant.Tenant?)null);
        await Invoke();

        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(TenantIdValue));
        await Invoke();

        _timing.Verify(t => t.NormalizeAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ForgotPassword_WhenAccountExists_NotificationCarriesTheRedemptionLink()
    {
        // G-270: el correo lleva un enlace que abre el canje con el codigo puesto. Sin el, el
        // titular tenia que copiar 43 caracteres a mano desde el mensaje.
        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(TenantIdValue));
        _enlaces
            .Setup(e => e.EnlaceDeCanjeDeContrasena(TenantCode, UserEmail, It.IsAny<string>()))
            .Returns("https://portal.evolith.test/login?recuperar=1&codigo=X");

        UmsNotification? enviada = null;
        _notifications
            .Setup(n => n.SendAsync(It.IsAny<UmsNotification>(), It.IsAny<CancellationToken>()))
            .Callback<UmsNotification, CancellationToken>((n, _) => enviada = n);

        await Invoke();

        Assert.NotNull(enviada);
        Assert.NotNull(enviada!.HtmlBody);
        Assert.Contains("portal.evolith.test", enviada.HtmlBody!, StringComparison.Ordinal);
        Assert.Contains("portal.evolith.test", enviada.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgotPassword_WithoutPortalUrl_StillNotifiesWithTheCodeAlone()
    {
        // El constructor de enlaces devuelve null cuando el despliegue no declara portal. Un
        // restablecimiento no puede quedarse sin enviar porque falte una comodidad.
        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(TenantIdValue));
        _enlaces
            .Setup(e => e.EnlaceDeCanjeDeContrasena(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string?)null);

        UmsNotification? enviada = null;
        _notifications
            .Setup(n => n.SendAsync(It.IsAny<UmsNotification>(), It.IsAny<CancellationToken>()))
            .Callback<UmsNotification, CancellationToken>((n, _) => enviada = n);

        await Invoke();

        Assert.NotNull(enviada);
        Assert.DoesNotContain("http", enviada!.Body, StringComparison.OrdinalIgnoreCase);
        // Sin enlace no hay boton, y el codigo sigue estando: es el mecanismo que de verdad importa.
        Assert.DoesNotContain("<a href", enviada.HtmlBody!, StringComparison.Ordinal);
        Assert.Contains("Consolas", enviada.HtmlBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgotPassword_WhenTheMailRelayFails_ResponseIsStillTheAmbiguousOne()
    {
        // G-270: al conectar un relay SMTP real, el adaptador PROPAGA el fallo de entrega —debe
        // hacerlo—. Pero ese fallo solo puede ocurrir en la rama «la cuenta existe», así que una
        // caída del relay convertiría este endpoint en un oráculo de enumeración: 500 para el
        // correo registrado, 200 para el desconocido.
        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(TenantIdValue));
        _notifications
            .Setup(n => n.SendAsync(It.IsAny<UmsNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Mail.SmtpException(System.Net.Mail.SmtpStatusCode.ServiceNotAvailable, "relay caido"));

        var conRelayCaido = await Invoke();

        GivenAccount(null);
        var sinCuenta = await Invoke("nadie@acme.com");

        Assert.True(conRelayCaido.IsSuccess);
        Assert.Equal(
            JsonSerializer.Serialize(sinCuenta.Value),
            JsonSerializer.Serialize(conRelayCaido.Value));
    }

    #endregion

    // =========================================================================
    #region La contraseña vigente sobrevive a la solicitud
    // =========================================================================

    [Fact]
    public async Task ForgotPassword_WhenAccountExists_LeavesTheCurrentPasswordUntouched()
    {
        GivenTenantExists();
        var user = MakeUserWithPassword(TenantIdValue);
        GivenAccount(user);

        await Invoke();

        var vigente = Assert.Single(user.PasswordCredentials);
        Assert.True(vigente.IsActive);
        Assert.Equal(ExistingHash, vigente.PasswordHash.GetValue());
    }

    [Fact]
    public async Task ForgotPassword_NeverPersistsTheUserAccount()
    {
        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(TenantIdValue));

        await Invoke();

        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    // =========================================================================
    #region Emisión del token
    // =========================================================================

    [Fact]
    public async Task ForgotPassword_WhenAccountExists_IssuesShortLivedTokenAndNotifiesTheMailbox()
    {
        GivenTenantExists();
        var user = MakeUserWithPassword(TenantIdValue);
        GivenAccount(user);

        await Invoke();

        _resetTokens.Verify(s => s.IssueAsync(
                TenantIdValue,
                user.Props.Id.GetValue(),
                It.Is<string>(hash => hash.Length == 64),   // SHA-256 hex: nunca el plaintext
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _notifications.Verify(n => n.SendAsync(
                It.Is<UmsNotification>(msg => msg.Recipient == UserEmail),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_WhenAccountBelongsToAnotherTenant_IssuesNothing()
    {
        GivenTenantExists();
        GivenAccount(MakeUserWithPassword(OtherTenantIdValue));

        await Invoke();

        VerifyNothingHappened();
    }

    [Fact]
    public async Task ForgotPassword_WhenAccountIsFederated_IssuesNothing()
    {
        GivenTenantExists();
        var federado = UserAccount.Create(
            TenantId.Load(TenantIdValue),
            Email.Create(UserEmail),
            UserCategory.Internal,
            IdentityReference.Create("keycloak|abc"),
            IdentityReferenceType.PartnerRef,
            ActorId.Create("sys")).Value;
        federado.Activate(ActorId.Create("sys"));
        GivenAccount(federado);

        await Invoke();

        VerifyNothingHappened();
    }

    [Fact]
    public async Task ForgotPassword_WhenAccountIsBlocked_IssuesNothing()
    {
        GivenTenantExists();
        var bloqueado = MakeUserWithPassword(TenantIdValue);
        bloqueado.Block(Reason.Create("prueba"), ActorId.Create("sys"));
        GivenAccount(bloqueado);

        await Invoke();

        VerifyNothingHappened();
    }

    [Fact]
    public async Task ForgotPassword_WhenTenantIsUnknown_DoesNotEvenLookUpTheAccount()
    {
        _tenantRepo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((Domain.Identity.Tenant.Tenant?)null);

        await Invoke();

        _userRepo.Verify(r => r.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingHappened();
    }

    private void VerifyNothingHappened()
    {
        _resetTokens.Verify(s => s.IssueAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notifications.Verify(n => n.SendAsync(It.IsAny<UmsNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
