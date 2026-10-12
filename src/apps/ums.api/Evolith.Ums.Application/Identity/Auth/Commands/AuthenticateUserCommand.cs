using Evolith.Ums.Domain.Identity.Auth;
using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Application.Identity.Auth.Commands;

/// <summary>
/// Authenticate a user by tenant code + credentials.
/// Returns a complete AuthorizationGraph on success.
///
/// FR-042 (ADR-UMS-097 §2.2): <see cref="SystemSuiteId"/> transporta la suite del contexto de
/// resolución del IdP. Hoy el <see cref="AccessScope"/> no fija suite y el inquilino no tiene
/// suite por defecto, así que queda <c>null</c> (sin filtro de suite); se enhebra ahora para que
/// una fuente de suite (slice posterior) la pueble sin volver a tocar el contrato. El dominio de
/// email se deriva del <see cref="Username"/> en el handler. No cambia el contrato REST de /auth/login.
/// </summary>
public sealed record AuthenticateUserCommand(
    string  TenantCode,
    string  Username,
    string  Password,
    string  ClientIp,
    AuthAccessScope AccessScope,
    bool    RememberMe = false,
    Guid?   SystemSuiteId = null,
    // ADR-0156 §3.2 — código del sistema que pide el grafo, OPCIONAL. Presente, el grafo se acota
    // a los perfiles del usuario en ese sistema; ausente, viajan todos (portal multiproducto).
    //
    // NO se reutiliza `SystemSuiteId` para esto: es el contexto de resolución de IdP de FR-042,
    // viaja como identificador y hoy siempre llega null. Mezclar ambas semánticas en un campo
    // ataría el enrutado de IdP al filtro de perfiles, que son problemas distintos (§3.3).
    string? SystemCode = null) : ICommand<AuthenticateUserResult>;

/// <summary>Result of a successful authentication — graph + raw JWT.</summary>
public sealed record AuthenticateUserResult(
    AuthorizationGraph Graph,
    string             Token,
    string             TokenType,
    int                ExpiresIn,      // seconds
    DateTime           IssuedAt,
    string             SerializedGraph,  // pre-serialized in the tenant's default format
    string             GraphFormat);     // "JSON" | "XML" | "YAML" | "CSV"
