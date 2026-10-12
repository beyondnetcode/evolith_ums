namespace Evolith.Ums.Infrastructure.Persistence.Identity.Entities;

using Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Registro de persistencia de un token de restablecimiento de contraseña (G-188).
///
/// <para><b>Seguridad:</b> se guarda ÚNICAMENTE el <see cref="TokenHash"/>. El plaintext sale una
/// sola vez hacia el buzón del titular y no se persiste, ni se registra en logs, ni viaja en
/// ninguna respuesta HTTP.</para>
/// </summary>
public class PasswordResetTokenRecord
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>SHA-256 (hex) del token en claro.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary><c>Active | Used | Invalidated</c>.</summary>
    public string Status { get; set; } = PasswordResetTokenStatuses.Active;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Momento en que el token dejó de ser canjeable, sea por canje o por invalidación.</summary>
    public DateTime? ConsumedAtUtc { get; set; }

    /// <summary>Motivo de la invalidación (reissue, password-reset, account-change).</summary>
    public string? InvalidatedReason { get; set; }
}
