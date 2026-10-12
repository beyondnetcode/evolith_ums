namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// Identifies the context in which an authentication request is made.
///
/// ADR-0077: Separates UMS portal management access from external API authentication.
/// - PortalManagement: user logs into the UMS web portal to manage their tenant data.
///   Always resolved via local BCrypt regardless of the tenant's IDP configuration.
/// - ExternalApi: alcance usado por el endpoint de sistemas satélite
///   (/api/v1/client/authenticate) **y también por el login del portal** (ver G-180 en
///   PortalManagement). Se resuelve por el IdP configurado del inquilino cuando
///   AUTH_USE_EXTERNAL_IDP = true.
/// </summary>
public enum AuthAccessScope
{
    /// <summary>
    /// Atajo de autenticación puramente local: `AuthMethodResolverService` devuelve BCrypt sin
    /// consultar la configuración de IdP del inquilino.
    ///
    /// AVISO (G-180): pese al nombre y a lo que decía esta documentación, **el login del portal
    /// NO usa este alcance**. `AuthEndpoints.HandleLoginAsync` construye el comando con
    /// <see cref="ExternalApi"/>, de modo que el portal SÍ pasa por la resolución de IdP del
    /// inquilino. Este valor no tiene hoy ningún llamador en producción.
    ///
    /// Se corrige la documentación y no el código a propósito: cambiar el alcance del portal a
    /// local alteraría la superficie de autenticación —dejaría fuera la federación para los
    /// usuarios del portal— y eso es una decisión de producto, no una errata. Quien la tome debe
    /// registrarla en `DECISIONS.md`; mientras tanto, quien razone sobre la superficie de ataque
    /// leyendo el dominio no debe concluir lo contrario de lo que ocurre.
    /// </summary>
    PortalManagement,

    /// <summary>
    /// External client API authentication (/api/v1/client/authenticate).
    /// Uses IDP when AUTH_USE_EXTERNAL_IDP = true for the tenant.
    /// </summary>
    ExternalApi,

    /// <summary>
    /// Internal administrative preview of the effective auth graph.
    /// Resolves the tenant's configured mode, but does not require an active
    /// Identity Provider to render the preview payload.
    /// </summary>
    InternalPreview,
}
