namespace Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

using Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Registro de persistencia de un refresh token (ADR-UMS-091 / FR-015/016).
///
/// <para><b>Seguridad:</b> se guarda ÚNICAMENTE el <see cref="TokenHash"/> (SHA-256 del
/// plaintext); el token en claro se devuelve una sola vez al cliente y nunca se persiste,
/// registra ni aparece en el grafo. La búsqueda en renovación/revocación se hace por hash.</para>
///
/// <para><see cref="FamilyId"/> agrupa la cadena de rotaciones de una misma sesión: la
/// detección de reuso invalida la familia entera. <see cref="Status"/> es
/// <c>Active | Rotated | Revoked | Used</c>.</para>
/// </summary>
public class RefreshTokenRecord
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Familia de la cadena de rotación (misma sesión).</summary>
    public Guid FamilyId { get; set; }

    /// <summary>
    /// Sesión (`sid`) que abrió el ingreso, G-295. La familia es el único recuerdo durable que le
    /// queda al canje del token opaco, que es anónimo: sin esta columna, el portador renovado salía
    /// con una sesión recién inventada y el logout —que revoca por `sid`— dejaba de alcanzarlo.
    ///
    /// <para>No se reutiliza <see cref="FamilyId"/> para esto aunque hoy haya una familia por
    /// sesión: el `sid` existe SIEMPRE y la familia solo cuando el inquilino activó la capacidad de
    /// refresh token, así que no son el mismo hecho. Nulo en los registros anteriores al cambio.</para>
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Código del sistema que el cliente PIDIÓ al ingresar (`LoginRequest.SystemCode`), G-297. Nulo
    /// cuando no pidió ninguno —portal multiproducto—, que es un valor con significado y no una
    /// ausencia de dato: obliga al desempate de siempre. No es el sistema RESUELTO (`sys_suite`),
    /// que es a dónde se cayó y no qué se pidió.
    /// </summary>
    public string? SystemCode { get; set; }

    /// <summary>SHA-256 (hex) del token en claro. Nunca se guarda el plaintext.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary><c>Active | Rotated | Revoked | Used</c>.</summary>
    public string Status { get; set; } = RefreshTokenStatuses.Active;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Nº de renovaciones ya realizadas en esta familia (tope opcional).</summary>
    public int RenewalCount { get; set; }

    /// <summary>Token que reemplazó a este al rotar (cadena de rotación).</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Motivo de revocación (logout, block, permission-change, reuse).</summary>
    public string? RevokedReason { get; set; }
}
