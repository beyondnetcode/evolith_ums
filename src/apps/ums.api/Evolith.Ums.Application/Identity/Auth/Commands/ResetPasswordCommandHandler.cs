using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>
/// Canje del token de restablecimiento: aquí —y solo aquí— cambia la contraseña (G-188).
///
/// <para>El token es la prueba de posesión del buzón, así que este es el primer punto del flujo
/// donde tocar la credencial es legítimo. Todo fallo colapsa en un único error: distinguir
/// «token inexistente» de «ya usado», «vencido» o «cuenta federada» reabriría por la puerta del
/// canje la enumeración que el flujo anónimo cierra.</para>
/// </summary>
public sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    /// <summary>Único error del canje. Ver la nota de clase: la ambigüedad es intencional.</summary>
    public const string InvalidTokenError =
        "AUTH_019: El enlace de restablecimiento no es válido o ha expirado.";

    private readonly IPasswordResetTokenStore _resetTokenStore;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly INotificationService _notificationService;
    private readonly ITenantRepository _tenantRepository;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenStore resetTokenStore,
        IUserAccountRepository userAccountRepository,
        IPasswordHashingService passwordHashingService,
        IRefreshTokenStore refreshTokenStore,
        INotificationService notificationService,
        ITenantRepository tenantRepository)
    {
        _resetTokenStore = resetTokenStore;
        _userAccountRepository = userAccountRepository;
        _passwordHashingService = passwordHashingService;
        _refreshTokenStore = refreshTokenStore;
        _notificationService = notificationService;
        _tenantRepository = tenantRepository;
    }

    public async Task<Result<ResetPasswordResponse>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Invalid();

        var now = DateTime.UtcNow;
        var snapshot = await _resetTokenStore
            .FindByHashAsync(PasswordResetToken.Hash(request.Token), cancellationToken)
            .ConfigureAwait(false);

        if (snapshot is null
            || snapshot.Status != PasswordResetTokenStatuses.Active
            || snapshot.ExpiresAtUtc <= now)
            return Invalid();

        var userAccount = await _userAccountRepository.GetByIdAsync(snapshot.UserId, cancellationToken).ConfigureAwait(false);
        if (userAccount is null
            || userAccount.Props.TenantId.GetValue() != snapshot.TenantId
            || userAccount.IdentityReference is not null
            || userAccount.Status == UserStatus.Blocked
            || userAccount.Status == UserStatus.Deleted
            || userAccount.Status == UserStatus.Denied)
        {
            // La cuenta cambió de estado entre la emisión y el canje: el token deja de servir y
            // se cierra, para que no quede vivo esperando a que la cuenta se reactive.
            await _resetTokenStore.ConsumeAsync(snapshot.Id, now, cancellationToken).ConfigureAwait(false);
            return Invalid();
        }

        // El actor del cambio es el propio titular: el restablecimiento lo ejecuta quien probó
        // poseer el buzón, no un administrador ni el sistema.
        var actor = ActorId.Create(snapshot.UserId.ToString());
        var hash = _passwordHashingService.Hash(request.NewPassword);
        var applied = userAccount.AddPassword(PasswordHash.Create(hash), actor);
        if (applied.IsFailure)
            return Result<ResetPasswordResponse>.Failure(applied.Error);

        await _userAccountRepository.UpdateAsync(userAccount, cancellationToken).ConfigureAwait(false);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken).ConfigureAwait(false);

        // Un solo uso: el token se gasta y cualquier otro que siguiera vivo para el mismo usuario
        // muere con él. El orden importa —primero la contraseña— para que un fallo al persistir
        // no deje al titular sin credencial nueva Y sin token para reintentar.
        await _resetTokenStore.ConsumeAsync(snapshot.Id, now, cancellationToken).ConfigureAwait(false);
        await _resetTokenStore.InvalidateActiveForUserAsync(
            snapshot.TenantId, snapshot.UserId, "password-reset", now, cancellationToken).ConfigureAwait(false);

        // Cambiar la contraseña sin cerrar las sesiones vivas dejaría dentro a quien la robó.
        // Solo se revocan los refresh tokens: la revocación de access tokens es por usuario y
        // ventana temporal (ITokenRevocationStore), así que aplicarla aquí impediría al titular
        // volver a entrar justo después de restablecer.
        await _refreshTokenStore.RevokeAllForUserAsync(
            snapshot.TenantId, snapshot.UserId, "password-reset", now, cancellationToken).ConfigureAwait(false);

        // El inquilino en el que se aplicó el cambio. La unicidad de correo se declara sobre
        // (TenantId, Email), asi que un mismo buzon puede tener cuenta en varios inquilinos y
        // «su contraseña fue actualizada» a secas no dice en CUAL. Quien tiene dos cuentas cambia
        // una, intenta entrar en la otra y recibe un 401 que nada explica: el login responde
        // AUTH_006 sin distinguir, a proposito, para no permitir enumeracion.
        //
        // Decirlo AQUI no la reabre: se nombra solo en el exito, y el exito exige haber canjeado
        // un token que prueba posesion del buzon. Quien lee esto ya podia saberlo.
        var tenantLabel = await ResolverInquilinoAsync(snapshot.TenantId, cancellationToken).ConfigureAwait(false);

        var email = userAccount.Email.GetValue();
        await _notificationService.SendAsync(
            NotificationTemplates.PasswordChanged(email, email.Split('@')[0], tenantLabel),
            cancellationToken).ConfigureAwait(false);

        return Result<ResetPasswordResponse>.Success(new ResetPasswordResponse(
            tenantLabel is null
                ? "Su contraseña fue actualizada. Ya puede iniciar sesión."
                : $"Su contraseña fue actualizada en el inquilino {tenantLabel}. Ya puede iniciar sesión ahí."));
    }

    private static Result<ResetPasswordResponse> Invalid() =>
        Result<ResetPasswordResponse>.Failure(InvalidTokenError);

    /// <summary>
    /// Etiqueta legible del inquilino, o <c>null</c> si no se puede resolver.
    ///
    /// <para>Nunca propaga el fallo: la contraseña YA se cambió y los tokens YA se gastaron.
    /// Romper aquí devolvería un error al titular por un cambio que sí se aplicó, y lo dejaría
    /// sin el aviso por correo. Perder la etiqueta degrada el mensaje; perder el canje, no.</para>
    /// </summary>
    private async Task<string?> ResolverInquilinoAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (tenant is null) return null;

            var code = tenant.Code.GetValue();
            var name = tenant.Name.GetValue();
            return string.IsNullOrWhiteSpace(name) || string.Equals(name, code, StringComparison.Ordinal)
                ? code
                : $"{code} ({name})";
        }
        catch
        {
            return null;
        }
    }
}
