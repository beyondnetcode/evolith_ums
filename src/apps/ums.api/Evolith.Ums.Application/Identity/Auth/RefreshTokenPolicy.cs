namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Política efectiva de refresh token de un inquilino, resuelta desde la
/// configuración jerárquica (ADR-UMS-091 / FR-015). Fail-closed: si la capacidad no
/// está habilitada, <see cref="Enabled"/> es <c>false</c> y el resto es irrelevante
/// (se conserva el modelo de ADR-UMS-088: solo <c>validUntil</c>).
/// </summary>
public sealed record RefreshTokenPolicy(
    bool Enabled,
    int LifetimeMinutes,
    bool Rotate,
    bool DetectReuse,
    int MaxRenewals)
{
    /// <summary>Política deshabilitada — comportamiento por defecto y fail-closed.</summary>
    public static readonly RefreshTokenPolicy Disabled =
        new(Enabled: false, LifetimeMinutes: 0, Rotate: false, DetectReuse: false, MaxRenewals: 0);
}
