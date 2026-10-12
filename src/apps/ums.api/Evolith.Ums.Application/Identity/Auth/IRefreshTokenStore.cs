namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Instantánea de un refresh token persistido, expuesta a la capa de aplicación sin
/// filtrar la entidad de EF. Nunca contiene el plaintext — la búsqueda es por hash.
/// </summary>
public sealed record RefreshTokenSnapshot(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    Guid FamilyId,
    string Status,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    int RenewalCount,
    // G-295 / G-297: lo que el ingreso sabía y la renovación perdía. La familia de refresco es el
    // ÚNICO recuerdo durable de la sesión que le queda al canje del token opaco: ese endpoint es
    // anónimo por diseño —el refresh token ES la credencial, y se usa justo cuando el portador ya
    // caducó—, así que no hay principal del que leer estos dos datos.
    //
    // Nulos en los registros emitidos antes de este cambio: se comportan como antes (sesión nueva,
    // grafo por desempate) en vez de romper una renovación viva.
    string? SessionId = null,
    string? SystemCode = null);

/// <summary>
/// Puerto de persistencia de refresh tokens (ADR-UMS-091 / FR-015/016). Guarda solo el
/// <b>hash</b> del token; el plaintext nunca cruza esta frontera hacia el almacén.
/// Aislado por inquilino. Cubre emisión, renovación (rotación) y revocación.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>
    /// Emite (persiste) un refresh token activo. <paramref name="tokenHash"/> es el SHA-256 del plaintext.
    ///
    /// <para><paramref name="sessionId"/> y <paramref name="systemCode"/> son OBLIGATORIOS y no
    /// tienen valor por omisión a propósito (G-295): un parámetro opcional es justo lo que permitió
    /// que cuatro emisores de portador olvidaran la sesión sin que nada se pusiera en rojo. Que sean
    /// nullables describe una ausencia real —una sesión anterior a que el `sid` existiera, un
    /// ingreso que no pidió sistema—, no un descuido del llamante.</para>
    /// </summary>
    Task IssueAsync(
        Guid tenantId,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        string? sessionId,
        string? systemCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un token por su hash, sea cual sea su estado. Devuelve <c>null</c> si no existe.
    /// Devolver tokens no-activos es intencional: permite detectar el <b>reuso</b> de un
    /// token ya rotado/revocado y responder invalidando la familia.
    /// </summary>
    Task<RefreshTokenSnapshot?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotación atómica: marca el token <paramref name="current"/> como <c>Rotated</c>
    /// (apuntando a <paramref name="newTokenId"/>) y persiste el nuevo token <c>Active</c>
    /// en la misma familia, con el contador de renovaciones incrementado en uno.
    ///
    /// <para>G-295/G-297: la sesión y el sistema pedido se HEREDAN de <paramref name="current"/>.
    /// No se reciben por parámetro porque no son del llamante: son de la familia, y una rotación no
    /// es una sesión nueva ni un cambio de producto. Si viajaran por parámetro, la enésima rotación
    /// podría contradecir a la primera.</para>
    /// </summary>
    Task RotateAsync(
        RefreshTokenSnapshot current,
        Guid newTokenId,
        string newTokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca <b>toda la familia</b> (todos los tokens no revocados con ese
    /// <paramref name="familyId"/>). Se usa en la detección de reuso y en la revocación
    /// explícita (logout, bloqueo, cambio crítico de permisos).
    /// </summary>
    Task RevokeFamilyAsync(
        Guid familyId,
        string reason,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoca <b>todas las familias vivas</b> de un usuario dentro de un inquilino
    /// (todos sus tokens no revocados). Es la revocación explícita del logout: cierra
    /// la sesión de refresh sin necesitar el <c>familyId</c>, que el logout no conoce.
    /// Idempotente: si no hay tokens activos, no hace nada.
    /// </summary>
    Task RevokeAllForUserAsync(
        Guid tenantId,
        Guid userId,
        string reason,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);
}
