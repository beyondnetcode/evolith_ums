namespace Evolith.Ums.Application.Identity.Auth;

/// <summary>
/// Resuelve la <see cref="RefreshTokenPolicy"/> efectiva de un inquilino desde la
/// configuración jerárquica (Global &gt; Suite &gt; Tenant &gt; Module), aplicando
/// una postura <b>fail-closed</b>: ante ausencia, error o valor inválido, la política
/// resultante está deshabilitada (ADR-UMS-091 / FR-015).
/// </summary>
public interface IRefreshTokenPolicyProvider
{
    RefreshTokenPolicy Resolve(Guid? tenantId, Guid? suiteId = null, Guid? moduleId = null);
}
