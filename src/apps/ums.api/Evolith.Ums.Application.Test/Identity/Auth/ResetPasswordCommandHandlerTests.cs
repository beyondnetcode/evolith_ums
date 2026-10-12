namespace Evolith.Ums.Application.Test.Identity.Auth;

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
/// G-188: el canje es el ÚNICO punto donde cambia la contraseña, y solo con un token vivo.
/// Todo lo demás —token inexistente, vencido, ya gastado, cuenta inhabilitada— colapsa en el
/// mismo error, sin tocar la credencial.
/// </summary>
public class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IPasswordResetTokenStore> _resetTokens = new();
    private readonly Mock<IUserAccountRepository> _userRepo = new();
    private readonly Mock<IPasswordHashingService> _hasher = new();
    private readonly Mock<IRefreshTokenStore> _refreshTokens = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITenantRepository> _tenantRepo = new();

    private static readonly Guid TenantIdValue = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid TokenId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private const string UserEmail = "admin@acme.com";
    private const string PreviousHash = "hash-de-la-clave-vigente";
    private const string NewHash = "hash-de-la-clave-nueva";
    private const string PlainToken = "token-en-claro";
    private const string NewPassword = "Nueva#Clave#2026";
    private const string TenantCode = "ACME";
    private const string TenantName = "Acme Corp";

    public ResetPasswordCommandHandlerTests()
    {
        _userRepo.Setup(r => r.UnitOfWork).Returns(_uow.Object);
        _uow.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns(NewHash);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(MakeTenant());
    }

    private static UserAccount MakeUserWithPassword(Guid tenantId = default)
    {
        var user = UserAccount.Create(
            TenantId.Load(tenantId == default ? TenantIdValue : tenantId),
            Email.Create(UserEmail),
            UserCategory.Internal,
            null, null,
            ActorId.Create("sys")).Value;
        user.Activate(ActorId.Create("sys"));
        user.AddPassword(PasswordHash.Create(PreviousHash), ActorId.Create("sys"));
        return user;
    }

    private static PasswordResetTokenSnapshot MakeSnapshot(
        Guid userId,
        string status = PasswordResetTokenStatuses.Active,
        int expiresInMinutes = 10) =>
        new(TokenId, TenantIdValue, userId, status,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(expiresInMinutes));

    private void GivenToken(PasswordResetTokenSnapshot? snapshot) =>
        _resetTokens.Setup(s => s.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(snapshot);

    private void GivenAccount(UserAccount? account) =>
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(account);

    private static Domain.Identity.Tenant.Tenant MakeTenant() =>
        Domain.Identity.Tenant.Tenant.Create(
            Code.Create(TenantCode), Name.Create(TenantName),
            OrganizationType.INTERNAL, ActorId.Create("sys"),
            tenantId: Domain.Kernel.ValueObjects.TenantId.Load(TenantIdValue)).Value;

    private ResetPasswordCommandHandler CreateHandler() =>
        new(_resetTokens.Object, _userRepo.Object, _hasher.Object, _refreshTokens.Object,
            _notifications.Object, _tenantRepo.Object);

    private Task<Result<ResetPasswordResponse>> Invoke() =>
        CreateHandler().Handle(new ResetPasswordCommand(PlainToken, NewPassword), CancellationToken.None);

    [Fact]
    public async Task ResetPassword_NombraElInquilinoEnLaRespuestaYEnElAviso()
    {
        // Un mismo correo puede tener cuenta en varios inquilinos, asi que «su contraseña fue
        // actualizada» a secas deja al titular sin saber DONDE entrar. El login no se lo va a
        // aclarar: responde AUTH_006 sin distinguir, a proposito.
        var user = MakeUserWithPassword();
        GivenToken(MakeSnapshot(user.Props.Id.GetValue()));
        GivenAccount(user);
        UmsNotification? enviada = null;
        _notifications.Setup(n => n.SendAsync(It.IsAny<UmsNotification>(), It.IsAny<CancellationToken>()))
                      .Callback<UmsNotification, CancellationToken>((n, _) => enviada = n)
                      .Returns(Task.CompletedTask);

        var result = await Invoke();

        Assert.True(result.IsSuccess);
        Assert.Contains(TenantCode, result.Value.Message, StringComparison.Ordinal);
        Assert.NotNull(enviada);
        Assert.Contains(TenantCode, enviada!.Body, StringComparison.Ordinal);
        Assert.Contains(TenantCode, enviada.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResetPassword_SiElInquilinoNoResuelve_CambiaLaClaveIgual()
    {
        // La contraseña YA se cambio y los tokens YA se gastaron cuando se resuelve la etiqueta.
        // Fallar aqui devolveria un error por un cambio que si se aplico. Se degrada el mensaje,
        // nunca el canje.
        var user = MakeUserWithPassword();
        GivenToken(MakeSnapshot(user.Props.Id.GetValue()));
        GivenAccount(user);
        _tenantRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("inquilino ilegible"));

        var result = await Invoke();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(TenantCode, result.Value.Message, StringComparison.Ordinal);
        var activa = Assert.Single(user.PasswordCredentials.Where(c => c.IsActive));
        Assert.Equal(NewHash, activa.PasswordHash.GetValue());
    }

    // =========================================================================
    #region Canje válido
    // =========================================================================

    [Fact]
    public async Task ResetPassword_WithLiveToken_ReplacesTheActiveCredential()
    {
        var user = MakeUserWithPassword();
        GivenToken(MakeSnapshot(user.Props.Id.GetValue()));
        GivenAccount(user);

        var result = await Invoke();

        Assert.True(result.IsSuccess);
        var activa = Assert.Single(user.PasswordCredentials.Where(c => c.IsActive));
        Assert.Equal(NewHash, activa.PasswordHash.GetValue());
        // La anterior no desaparece: queda desactivada, que es como el agregado lleva su historia.
        Assert.Contains(user.PasswordCredentials, c => !c.IsActive && c.PasswordHash.GetValue() == PreviousHash);
    }

    [Fact]
    public async Task ResetPassword_WithLiveToken_PersistsAndBurnsTheToken()
    {
        var user = MakeUserWithPassword();
        GivenToken(MakeSnapshot(user.Props.Id.GetValue()));
        GivenAccount(user);

        await Invoke();

        _userRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _resetTokens.Verify(s => s.ConsumeAsync(TokenId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _resetTokens.Verify(s => s.InvalidateActiveForUserAsync(
                TenantIdValue, user.Props.Id.GetValue(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WithLiveToken_ClosesLiveSessionsAndNotifiesTheOwner()
    {
        var user = MakeUserWithPassword();
        GivenToken(MakeSnapshot(user.Props.Id.GetValue()));
        GivenAccount(user);

        await Invoke();

        _refreshTokens.Verify(s => s.RevokeAllForUserAsync(
                TenantIdValue, user.Props.Id.GetValue(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _notifications.Verify(n => n.SendAsync(
                It.Is<UmsNotification>(msg => msg.Recipient == UserEmail),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    // =========================================================================
    #region Canje rechazado — la credencial vigente sobrevive
    // =========================================================================

    [Theory]
    [InlineData("inexistente")]
    [InlineData("vencido")]
    [InlineData("gastado")]
    [InlineData("invalidado")]
    public async Task ResetPassword_WithUnusableToken_FailsWithTheSameErrorAndChangesNothing(string caso)
    {
        var user = MakeUserWithPassword();
        var userId = user.Props.Id.GetValue();
        GivenAccount(user);
        GivenToken(caso switch
        {
            "inexistente" => null,
            "vencido" => MakeSnapshot(userId, expiresInMinutes: -1),
            "gastado" => MakeSnapshot(userId, PasswordResetTokenStatuses.Used),
            _ => MakeSnapshot(userId, PasswordResetTokenStatuses.Invalidated),
        });

        var result = await Invoke();

        Assert.True(result.IsFailure);
        Assert.Equal(ResetPasswordCommandHandler.InvalidTokenError, result.Error);
        VerifyCredentialSurvived(user);
    }

    [Fact]
    public async Task ResetPassword_WithEmptyToken_FailsWithoutTouchingTheStore()
    {
        var result = await CreateHandler().Handle(
            new ResetPasswordCommand("   ", NewPassword), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ResetPasswordCommandHandler.InvalidTokenError, result.Error);
        _resetTokens.Verify(s => s.FindByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_WhenTokenPointsToAnotherTenantAccount_FailsAndBurnsTheToken()
    {
        var user = MakeUserWithPassword(Guid.Parse("9c1c2b3a-0000-4000-8000-000000000099"));
        GivenToken(MakeSnapshot(user.Props.Id.GetValue()));
        GivenAccount(user);

        var result = await Invoke();

        Assert.True(result.IsFailure);
        Assert.Equal(ResetPasswordCommandHandler.InvalidTokenError, result.Error);
        VerifyCredentialSurvived(user);
        // El token queda gastado: si la cuenta ya no es la que era, ese secreto no debe seguir vivo.
        _resetTokens.Verify(s => s.ConsumeAsync(TokenId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_WhenAccountWasBlockedAfterIssuing_Fails()
    {
        var user = MakeUserWithPassword();
        user.Block(Reason.Create("prueba"), ActorId.Create("sys"));
        GivenToken(MakeSnapshot(user.Props.Id.GetValue()));
        GivenAccount(user);

        var result = await Invoke();

        Assert.True(result.IsFailure);
        Assert.Equal(ResetPasswordCommandHandler.InvalidTokenError, result.Error);
        VerifyCredentialSurvived(user);
    }

    [Fact]
    public async Task ResetPassword_WhenAccountVanished_Fails()
    {
        GivenToken(MakeSnapshot(Guid.NewGuid()));
        GivenAccount(null);

        var result = await Invoke();

        Assert.True(result.IsFailure);
        Assert.Equal(ResetPasswordCommandHandler.InvalidTokenError, result.Error);
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void VerifyCredentialSurvived(UserAccount user)
    {
        var activa = Assert.Single(user.PasswordCredentials.Where(c => c.IsActive));
        Assert.Equal(PreviousHash, activa.PasswordHash.GetValue());
        _userRepo.Verify(r => r.UpdateAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _refreshTokens.Verify(s => s.RevokeAllForUserAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
