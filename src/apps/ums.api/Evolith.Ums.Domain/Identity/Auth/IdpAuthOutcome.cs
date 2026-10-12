namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// Clasificación del resultado de un intento de autenticación contra un IdP federado,
/// para el fallback encadenado de FR-042 (ADR-UMS-097 §2.3). Es la <b>decisión de seguridad</b>
/// que gobierna si la cadena de <c>FallbackToId</c> AVANZA o se DETIENE:
/// <list type="bullet">
///   <item><see cref="Success"/>: el IdP validó la credencial y devolvió una identidad externa.</item>
///   <item><see cref="InfraUnavailable"/>: indisponibilidad de <b>infraestructura</b> del IdP
///     (timeout, 5xx, JWKS inalcanzable, adaptador no registrado). Es el <b>único</b> caso que
///     autoriza avanzar al siguiente proveedor de la cadena.</item>
///   <item><see cref="CredentialTerminal"/>: el IdP respondió y rechazó (credencial inválida,
///     token no válido, política) <b>o</b> el fallo no se puede clasificar con certeza como
///     infraestructura. Es <b>TERMINAL</b>: NUNCA avanza la cadena. Encadenar ante un fallo de
///     credenciales permitiría <i>credential spraying</i> cross-IdP; por eso, y por <i>fail-closed</i>,
///     ante la duda se detiene el intento (ADR-UMS-097 §2.3).</item>
/// </list>
/// </summary>
public enum IdpAuthOutcome
{
    Success,
    InfraUnavailable,
    CredentialTerminal,
}
