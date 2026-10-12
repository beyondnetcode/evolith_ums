namespace Evolith.Ums.Presentation.Middleware;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

/// <summary>
/// HARDENING-03: Rejects requests from users whose tokens have been revoked.
///
/// <para>G-247: comprueba DOS cosas distintas, y son distintas a propósito. La cuenta puede estar
/// vetada entera —bloqueo, borrado, cambio de contraseña—, y eso lo responde
/// <c>ITokenRevocationStore</c>, que revoca por usuario. O puede estar cerrada solo ESTA sesión
/// —alguien pulsó «cerrar sesión» en este dispositivo—, y eso lo responde
/// <c>ISessionRevocationStore</c>, que revoca por `sid`. Mezclarlas en una sola clave obligaría a
/// elegir: o el logout echa al usuario de todos sus dispositivos, o el bloqueo de una cuenta deja
/// vivas las sesiones ya abiertas.</para>
///
/// Runs after UseAuthentication so <c>HttpContext.User</c> is already populated.
/// Returns HTTP 401 with a Problem Details body when the authenticated user is in
/// the revocation list (deleted or blocked). This ensures that deleting or blocking
/// a user takes effect immediately, without waiting for JWT expiry.
///
/// Registration order (Program.cs):
///   UseAuthentication → UseAuthorization → UseTokenRevocation → ...
/// </summary>
public sealed class TokenRevocationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITokenRevocationStore revocationStore,
        ISessionRevocationStore sessionRevocationStore)
    {
        // Only check authenticated requests — anonymous routes pass through.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? context.User.FindFirst("sub")?.Value;

            // G-247: la sesión de este portador. Los emitidos antes de que existiera `sid` no lo
            // llevan: siguen valiendo hasta caducar, que es lo mismo que hacían antes. Rechazarlos
            // cerraría de golpe todas las sesiones vivas en el despliegue del cambio.
            var sessionId = context.User.FindFirst("sid")?.Value;

            var vetado = !string.IsNullOrEmpty(userId)
                && await revocationStore.IsRevokedAsync(userId, context.RequestAborted);
            var sesionCerrada = !vetado
                && !string.IsNullOrEmpty(sessionId)
                && await sessionRevocationStore.EstaRevocadaAsync(sessionId, context.RequestAborted);

            if (vetado || sesionCerrada)
            {
                context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type     = "https://httpstatuses.io/401",
                    title    = "Unauthorized",
                    status   = 401,
                    detail   = "Your session has been revoked. Please sign in again.",
                    traceId  = context.TraceIdentifier,
                });
                return;
            }
        }

        await next(context);
    }
}

public static class TokenRevocationMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenRevocation(this IApplicationBuilder app)
        => app.UseMiddleware<TokenRevocationMiddleware>();
}
