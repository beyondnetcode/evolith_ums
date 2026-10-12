namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Estados canónicos del ciclo de vida de un refresh token (ADR-UMS-091).
///
/// <list type="bullet">
///   <item><b>Active</b>: vigente; el único que renueva.</item>
///   <item><b>Rotated</b>: fue canjeado y reemplazado por otro de su familia (rotación normal).</item>
///   <item><b>Used</b>: consumido sin rotación (política sin rotación).</item>
///   <item><b>Revoked</b>: invalidado (logout, bloqueo, cambio de permisos o detección de reuso).</item>
/// </list>
///
/// Definidos en la capa de aplicación para que el resolutor y el almacén (infraestructura)
/// compartan la misma verdad sin que aplicación dependa de infraestructura.
/// </summary>
public static class RefreshTokenStatuses
{
    public const string Active = "Active";
    public const string Rotated = "Rotated";
    public const string Revoked = "Revoked";
    public const string Used = "Used";
}
