using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>
/// Renueva una sesión de PORTAL a partir del principal ya autenticado por cookie
/// (D-019 / ADR-UMS-091). A diferencia de <see cref="RefreshAuthenticationCommand"/> —que
/// canjea un refresh token OPACO (ADR-UMS-091/FR-015)—, este comando parte de la cookie de
/// sesión viva: recibe el <c>userId</c> y el <c>tenantId</c> resueltos de los claims y
/// <b>regenera el grafo de autorización COMPLETO</b> desde el estado actual, en espejo del
/// login (<see cref="AuthenticateUserCommandHandler"/>).
///
/// Cierra la brecha de latencia de permisos del refresh deslizante: el JWT emitido tras la
/// renovación porta el modelo de permisos vigente, no un grafo vacío.
/// </summary>
public sealed record RefreshSessionCommand(
    Guid   UserId,
    Guid   TenantId,
    string ClientIp,
    // G-297 — el sistema que el cliente pidió AL INGRESAR, tal como lo recuerda su propia
    // credencial (claim `req_sys` de la cookie o del portador). Renovar sin él devolvía el grafo
    // por desempate, así que un sujeto con perfil en dos sistemas —`admin@evolith.example` los tiene
    // en SIL y en UMS— podía entrar en UMS y salir de la renovación en SIL, mientras la SPA seguía
    // pintando el menú de UMS porque solo reemplaza el token, no el grafo.
    //
    // Null significa «el ingreso no pidió sistema» —el portal multiproducto—, y entonces el
    // desempate de siempre es la respuesta correcta. Por eso NO se propaga el `sys_suite` resuelto:
    // ese es el sistema en el que se CAYÓ, no el que se PIDIÓ, y usarlo acotaría el bloque
    // `profiles` de una sesión que nunca estuvo acotada, escondiéndole al portal la mitad de los
    // sombreros que su usuario tiene.
    string? SystemCode = null) : ICommand<RefreshSessionResult>;

/// <summary>Resultado de una renovación de sesión por cookie — grafo recién regenerado.</summary>
public sealed record RefreshSessionResult(
    AuthorizationGraph Graph,
    string             GraphFormat,
    int                ExpiresIn);   // vida del access token, en segundos (derivada del grafo)
