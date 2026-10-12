namespace Evolith.Ums.Domain.Identity.Auth;

/// <summary>
/// Clasificador <b>explícito</b> y <b>fail-closed</b> del resultado de un intento de autenticación
/// contra un IdP federado (FR-042 · ADR-UMS-097 §2.3). Traduce el <see cref="Result{TValue}"/> que
/// devuelve el path IdP (<c>IIdpAuthStrategy</c>/adaptadores) a un <see cref="IdpAuthOutcome"/>,
/// que es lo que decide si el fallback encadenado avanza o se detiene.
///
/// <para><b>Señales de indisponibilidad de infraestructura</b> (las únicas que autorizan avanzar),
/// derivadas del código real del path IdP y ambas listadas por ADR-UMS-097 §2.3:</para>
/// <list type="bullet">
///   <item><c>AUTH_012</c> — <c>IdpAuthStrategyDispatcher</c>: no hay adaptador registrado para la
///     estrategia del proveedor («adaptador no disponible/no registrado»).</item>
///   <item><c>AUTH_034</c> — <c>HttpJwksProvider</c>: no se pudo obtener el JWKS del issuer
///     (5xx/timeout/red) («error 5xx del IdP/JWKS»).</item>
///   <item><c>AUTH_035</c> — <c>HttpOidcTokenClient</c>: el <i>token endpoint</i> respondió <c>5xx</c>, dio
///     timeout o falló el transporte (G-108). Es la rama de INFRA del intercambio de código, ahora
///     estructuralmente separada del <c>4xx</c> de credencial (que sigue en <c>AUTH_021</c>).</item>
/// </list>
///
/// <para><b>Todo lo demás es TERMINAL</b> (no avanza), por <i>fail-closed</i>. En particular:</para>
/// <list type="bullet">
///   <item>rechazos de credencial/token del IdP (<c>AUTH_02x</c>: firma, iss/aud/exp/nonce, etc.);</item>
///   <item><c>AUTH_021</c> (intercambio de código): <b>4xx</b> del token endpoint (<c>invalid_grant</c> u
///     otra respuesta de cliente) o cuerpo <c>2xx</c> malformado ⇒ TERMINAL. NUNCA debe entrar en la lista
///     blanca de infra: encadenar ante un fallo de credencial abriría <i>credential spraying</i> (§2.3);</item>
///   <item><c>AUTH_013</c> — código reutilizado por flujos no-IdP (stub de dev, contexto de gestión),
///     ambiguo ⇒ terminal;</item>
///   <item>códigos desconocidos o error sin código ⇒ terminal.</item>
/// </list>
///
/// <para><b>Ambigüedad resuelta (G-108):</b> el disparo de fallback por «timeout/5xx del IdP» en el
/// <i>token endpoint</i> ya se puede activar con seguridad porque <c>HttpOidcTokenClient</c> emite un código
/// de infra <b>estructuralmente distinto</b> —<c>AUTH_035</c>, por clase de status HTTP / tipo de excepción,
/// no por texto— del <c>4xx</c> de credencial (<c>AUTH_021</c>, que sigue terminal). El <c>4xx</c>
/// <c>invalid_grant</c> permanece TERMINAL bajo cualquier circunstancia: la separación es la que cierra el
/// vector de <i>credential spraying</i> (ADR-UMS-097 §2.3).</para>
/// </summary>
public static class IdpAuthOutcomeClassifier
{
    /// <summary>
    /// Lista blanca de códigos de error que se consideran indisponibilidad de infraestructura
    /// (y por tanto autorizan avanzar la cadena). Cualquier código ausente de este conjunto es TERMINAL.
    /// </summary>
    public static readonly IReadOnlySet<string> InfraUnavailableCodes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "AUTH_012", // dispatcher: sin adaptador registrado para la estrategia
            "AUTH_034", // JWKS inalcanzable (5xx/timeout/red)
            "AUTH_035", // token endpoint OIDC inalcanzable: 5xx/timeout/transporte (G-108). SOLO la rama de
                        // infra de HttpOidcTokenClient lo emite; el 4xx invalid_grant sigue en AUTH_021
                        // (TERMINAL), jamás aquí ⇒ no abre credential spraying (ADR-UMS-097 §2.3).
        };

    /// <summary>
    /// Clasifica el resultado de un intento IdP. <see cref="Result{TValue}.IsSuccess"/> ⇒
    /// <see cref="IdpAuthOutcome.Success"/>; un fallo cuyo código está en la lista blanca de infra ⇒
    /// <see cref="IdpAuthOutcome.InfraUnavailable"/>; cualquier otro fallo ⇒
    /// <see cref="IdpAuthOutcome.CredentialTerminal"/> (fail-closed).
    /// </summary>
    public static IdpAuthOutcome Classify(Result<ExternalIdentity> attempt)
    {
        if (attempt is null)
        {
            return IdpAuthOutcome.CredentialTerminal;
        }

        if (attempt.IsSuccess)
        {
            return IdpAuthOutcome.Success;
        }

        return IsInfraUnavailable(attempt.Error)
            ? IdpAuthOutcome.InfraUnavailable
            : IdpAuthOutcome.CredentialTerminal;
    }

    /// <summary>
    /// Indica si el error corresponde a una señal INEQUÍVOCA de indisponibilidad de infraestructura.
    /// Cualquier ambigüedad devuelve <c>false</c> (fail-closed).
    /// </summary>
    public static bool IsInfraUnavailable(string? error)
        => !string.IsNullOrWhiteSpace(error) && InfraUnavailableCodes.Contains(ExtractCode(error));

    /// <summary>Extrae el prefijo <c>AUTH_0xx</c> del mensaje de error (todo antes del primer «:»).</summary>
    private static string ExtractCode(string error)
    {
        var separatorIndex = error.IndexOf(':');
        return (separatorIndex > 0 ? error[..separatorIndex] : error).Trim();
    }
}
