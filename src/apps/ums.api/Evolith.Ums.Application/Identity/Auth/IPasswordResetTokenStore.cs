namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Estados de un token de restablecimiento. <c>Active</c> es el único canjeable; el resto
/// existen para que el canje distinga «nunca emitido» de «ya gastado» en la auditoría, sin
/// que esa distinción llegue jamás al llamante.
/// </summary>
public static class PasswordResetTokenStatuses
{
    public const string Active = "Active";
    public const string Used = "Used";
    public const string Invalidated = "Invalidated";
}

/// <summary>
/// Instantánea de un token de restablecimiento persistido. Nunca contiene el plaintext:
/// la búsqueda es por hash, igual que con los refresh tokens.
/// </summary>
public sealed record PasswordResetTokenSnapshot(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Status,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

/// <summary>
/// Puerto de persistencia de tokens de restablecimiento de contraseña.
///
/// <para>Existe porque la petición anónima de «olvidé mi contraseña» <b>no debe cambiar la
/// credencial</b>: quien conoce un correo no prueba con ello poseer el buzón. El único efecto
/// legítimo de esa petición es emitir un secreto de un solo uso y vida corta hacia el canal
/// de notificación; la contraseña anterior sigue siendo válida hasta que ese secreto se canjea.</para>
///
/// <para>Se guarda ÚNICAMENTE el hash SHA-256 del token: un volcado de la tabla no permite
/// tomar ninguna cuenta.</para>
/// </summary>
public interface IPasswordResetTokenStore
{
    /// <summary>
    /// Emite un token activo. La implementación invalida primero los tokens vivos del mismo
    /// usuario: solo un secreto de restablecimiento puede estar pendiente a la vez, de modo que
    /// pedir el restablecimiento otra vez anula el enlace anterior.
    /// </summary>
    Task IssueAsync(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un token por su hash, sea cual sea su estado. Devolver también los no activos es
    /// intencional: permite que el canje trate «reutilizado» y «revocado» exactamente igual que
    /// «inexistente» de cara al llamante, sin perder la traza interna.
    /// </summary>
    Task<PasswordResetTokenSnapshot?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca el token como gastado. Debe invocarse en el canje y solo una vez: es lo que
    /// convierte el enlace en «de un solo uso».
    /// </summary>
    Task ConsumeAsync(
        Guid tokenId,
        DateTime consumedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida todos los tokens vivos del usuario. Se usa tras un canje exitoso y ante un
    /// cambio de credencial por otra vía. Idempotente.
    /// </summary>
    Task InvalidateActiveForUserAsync(
        Guid tenantId,
        Guid userId,
        string reason,
        DateTime invalidatedAtUtc,
        CancellationToken cancellationToken = default);
}
