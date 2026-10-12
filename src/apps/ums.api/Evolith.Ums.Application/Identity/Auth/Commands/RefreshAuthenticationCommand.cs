using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>
/// Renueva una sesión a partir de un refresh token (ADR-UMS-091 / FR-015).
///
/// Regenera el <b>grafo completo</b> desde el estado actual (cierra la brecha de
/// latencia de permisos), rota el token (rotación + detección de reuso) y aplica la
/// postura fail-closed: si el inquilino no tiene la capacidad activa, la renovación
/// se rechaza.
/// </summary>
public sealed record RefreshAuthenticationCommand(
    string RefreshToken,
    string ClientIp) : ICommand<RefreshAuthenticationResult>;

/// <summary>Resultado de una renovación exitosa — grafo recién regenerado + nuevo refresh.</summary>
public sealed record RefreshAuthenticationResult(
    AuthorizationGraph Graph,
    string             SerializedGraph,
    string             GraphFormat,
    int                ExpiresIn,          // vida del access token, en segundos
    DateTime           IssuedAt,
    string?            NewRefreshToken,    // null si la política no rota (el cliente conserva el suyo)
    int                RefreshExpiresIn,   // vida del refresh, en segundos
    // G-295 — la sesión (`sid`) que la familia de refresco recuerda desde el ingreso. Sube hasta
    // aquí porque quien acuña el portador es la capa de presentación, y sin este dato acuñaba una
    // sesión INVENTADA: el portador renovado dejaba de estar atado a la sesión que el logout
    // revoca, de modo que cerrar sesión respondía 200 y no cerraba nada.
    //
    // Null solo en familias emitidas antes de este cambio.
    string?            SessionId = null);
