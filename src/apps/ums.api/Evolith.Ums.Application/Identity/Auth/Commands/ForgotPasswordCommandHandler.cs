using System.Diagnostics;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Common.Notifications;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>
/// Solicitud anónima de restablecimiento (G-188).
///
/// <para><b>Lo que este flujo NO hace, y por qué:</b> no cambia la contraseña ni devuelve
/// credencial alguna. Conocer un correo no prueba poseer el buzón, así que actuar sobre la
/// credencial ante una petición anónima permitía a cualquiera dejar fuera al titular —o entrar
/// en su lugar si la contraseña nueva viajaba en la respuesta—. Lo único que hace es emitir un
/// secreto de un solo uso y vida corta y mandarlo por el buzón; el cambio ocurre en el canje
/// (<see cref="ResetPasswordCommandHandler"/>), que es donde ya hay prueba de posesión.</para>
///
/// <para><b>Indistinguibilidad:</b> la respuesta es literalmente la misma instancia lógica para
/// cuenta existente e inexistente —un solo campo con texto fijo— y el tiempo se nivela con
/// <see cref="IResponseTimingNormalizer"/>, porque el camino «existe» escribe y notifica mientras
/// el otro retorna en seco.</para>
/// </summary>
public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, ForgotPasswordResponse>
{
    /// <summary>
    /// Texto único de la respuesta. Es constante y deliberadamente vago: no confirma ni desmiente
    /// que el correo esté registrado.
    /// </summary>
    private const string AmbiguousMessage =
        "Si el correo está registrado, recibirá instrucciones para restablecer su contraseña.";

    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordResetTokenStore _resetTokenStore;
    private readonly INotificationService _notificationService;
    private readonly IResponseTimingNormalizer _timingNormalizer;
    private readonly INotificationLinkBuilder _linkBuilder;

    public ForgotPasswordCommandHandler(
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository,
        IPasswordResetTokenStore resetTokenStore,
        INotificationService notificationService,
        IResponseTimingNormalizer timingNormalizer,
        INotificationLinkBuilder linkBuilder)
    {
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
        _resetTokenStore = resetTokenStore;
        _notificationService = notificationService;
        _timingNormalizer = timingNormalizer;
        _linkBuilder = linkBuilder;
    }

    public async Task<Result<ForgotPasswordResponse>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        // El resultado del intento se descarta a propósito: nada de lo que ocurra aquí dentro
        // puede alterar lo que ve el llamante.
        //
        // El catch NO es defensivo por costumbre, y apareció al conectar un relay SMTP real
        // (G-270). El adaptador de correo PROPAGA el fallo de entrega —debe hacerlo: dar por
        // enviado lo que no salió es el defecto que cierra—, pero aquí ese fallo solo ocurre en
        // la rama «la cuenta existe»: una caída del relay convertiría este endpoint en un oráculo
        // de enumeración, 500 para el correo registrado y 200 para el desconocido. Quien registra
        // el fallo con su excepción es el adaptador, antes de lanzarlo; aquí se traga para no
        // decir por el código de estado lo que la respuesta calla.
        try
        {
            await TryIssueResetTokenAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Intencionadamente vacío: ver arriba.
        }
        await _timingNormalizer.NormalizeAsync(startedAt, cancellationToken).ConfigureAwait(false);

        return Result<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(AmbiguousMessage));
    }

    private async Task TryIssueResetTokenAsync(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByCodeAsync(request.TenantCode, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
            return;

        var userAccount = await _userAccountRepository
            .GetByEmailAsync(Email.Create(request.Email), cancellationToken)
            .ConfigureAwait(false);

        if (userAccount is null || userAccount.Props.TenantId.GetValue() != tenant.Props.Id.GetValue())
            return;

        // Las cuentas federadas no tienen contraseña local que restablecer, y las bloqueadas o
        // borradas no deben recuperar acceso por esta vía: en ambos casos el desenlace de cara
        // al llamante es idéntico al de un correo desconocido.
        if (userAccount.IdentityReference is not null)
            return;

        if (userAccount.Status == UserStatus.Blocked
            || userAccount.Status == UserStatus.Deleted
            || userAccount.Status == UserStatus.Denied)
            return;

        var token = PasswordResetToken.Generate();
        var issuedAt = DateTime.UtcNow;

        await _resetTokenStore.IssueAsync(
            tenant.Props.Id.GetValue(),
            userAccount.Props.Id.GetValue(),
            PasswordResetToken.Hash(token),
            issuedAt,
            issuedAt.Add(PasswordResetToken.Lifetime),
            cancellationToken).ConfigureAwait(false);

        var email = userAccount.Email.GetValue();

        // El nombre que ve el titular. Antes salia la parte local del correo —«Hola aarroyo»—
        // porque era lo unico que este handler tenia a mano; la cuenta lleva su nombre para
        // mostrar y es el que corresponde. Si no lo tiene, se cae a la parte local, que sigue
        // siendo mejor que un saludo sin nombre.
        var nombre = userAccount.DisplayName?.GetValue() is { Length: > 0 } mostrado
            ? mostrado
            : email.Split('@')[0];

        await _notificationService.SendAsync(
            NotificationTemplates.PasswordResetRequested(
                recipient: email,
                recipientName: nombre,
                resetToken: token,
                expiresInMinutes: (int)PasswordResetToken.Lifetime.TotalMinutes,
                enlaceCanje: _linkBuilder.EnlaceDeCanjeDeContrasena(request.TenantCode, email, token)),
            cancellationToken).ConfigureAwait(false);
    }
}
