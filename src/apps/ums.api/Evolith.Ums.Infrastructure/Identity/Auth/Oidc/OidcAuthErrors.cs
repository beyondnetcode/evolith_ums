namespace Evolith.Ums.Infrastructure.Identity.Auth.Oidc;

/// <summary>
/// Códigos y mensajes de error del flujo OIDC (Result Pattern, textos en español).
/// Convergen con la normalización Result→HTTP (G-045): fallos de configuración /
/// validación → 401/409, nunca 503 ni excepción (ADR-UMS-094).
/// </summary>
public static class OidcAuthErrors
{
    public static string ConfigNotFound(string detalle) =>
        $"AUTH_020: No se encontró configuración OIDC válida para el proveedor. {detalle}";

    public static string CallbackInvalid(string detalle) =>
        $"AUTH_033: Callback OIDC inválido. {detalle}";

    public const string StateMismatch =
        "AUTH_033: El parámetro 'state' del callback no coincide con el emitido en la autorización.";

    /// <summary>
    /// AUTH_021 — fallo del intercambio de código atribuible al CLIENTE/CREDENCIAL: respuesta 4xx del
    /// token endpoint (p. ej. <c>invalid_grant</c>) o cuerpo 2xx malformado. Es TERMINAL: nunca debe
    /// entrar en la lista blanca de infra del clasificador, porque encadenar el fallback ante un fallo de
    /// credencial abriría <i>credential spraying</i> cross-IdP (invariante irrenunciable de ADR-UMS-097 §2.3).
    /// La rama de INFRAESTRUCTURA (5xx/timeout/transporte) usa un código estructuralmente distinto
    /// (<see cref="TokenEndpointUnavailable"/> = AUTH_035); ver G-108.
    /// </summary>
    public static string TokenExchangeFailed(string detalle) =>
        $"AUTH_021: Falló el intercambio del código por tokens contra el IdP. {detalle}";

    /// <summary>
    /// AUTH_035 — INDISPONIBILIDAD de infraestructura del token endpoint del IdP: respuesta 5xx, timeout o
    /// fallo de transporte (conexión/red/DNS). Es <b>estructuralmente distinto</b> de
    /// <see cref="TokenExchangeFailed"/> (AUTH_021 = 4xx/credencial → TERMINAL): esa separación por clase de
    /// status HTTP / tipo de excepción —no por texto— es la que permite habilitar el fallback encadenado
    /// SOLO por infra (ADR-UMS-097 §2.3, G-108) sin abrir <i>credential spraying</i>. Se añade a la lista
    /// blanca <c>IdpAuthOutcomeClassifier.InfraUnavailableCodes</c> y mapea a HTTP 503.
    ///
    /// <para>Se eligió un código NUEVO en la familia OIDC (AUTH_020..AUTH_034; local a este flujo, no vive
    /// en el catálogo SDK) en lugar de reutilizar <c>AUTH_013</c>=IdpCallFailed: AUTH_013 está contaminado
    /// por un rechazo de credencial del stub de dev (<c>StubIdpAuthAdapter</c>) y por errores de gestión,
    /// de modo que reusarlo para infra arriesgaría clasificar un rechazo de credencial como infra
    /// (regresión de seguridad). AUTH_035 no colisiona con ninguna semántica previa (G-108).</para>
    /// </summary>
    public static string TokenEndpointUnavailable(string detalle) =>
        $"AUTH_035: El token endpoint del IdP no está disponible (5xx/timeout/transporte). {detalle}";

    public const string MissingIdToken =
        "AUTH_022: La respuesta del token endpoint no contiene 'id_token'.";

    public const string MalformedIdToken =
        "AUTH_023: El 'id_token' no tiene el formato JWT esperado (cabecera.carga.firma).";

    public static string UnsupportedAlgorithm(string? alg) =>
        $"AUTH_024: Algoritmo de firma no soportado ('{alg ?? "none"}'). Se exige RS256.";

    public static string SigningKeyNotFound(string? kid) =>
        $"AUTH_025: No se encontró la llave de firma (kid='{kid ?? "-"}') en el JWKS del issuer.";

    public const string InvalidSignature =
        "AUTH_026: La firma del 'id_token' es inválida.";

    public const string InvalidIssuer =
        "AUTH_027: El 'iss' del 'id_token' no coincide con el issuer configurado.";

    public const string InvalidAudience =
        "AUTH_028: El 'aud' del 'id_token' no incluye el client_id configurado.";

    public const string Expired =
        "AUTH_029: El 'id_token' está expirado ('exp').";

    public const string NotYetValid =
        "AUTH_030: El 'id_token' aún no es válido ('nbf').";

    public const string NonceMismatch =
        "AUTH_031: El 'nonce' del 'id_token' no coincide con el emitido en la autorización.";

    public const string MissingEmail =
        "AUTH_032: El 'id_token' no contiene el claim 'email'; no se puede mapear la identidad UMS.";

    public static string JwksFetchFailed(string detalle) =>
        $"AUTH_034: No se pudo obtener el JWKS del issuer. {detalle}";
}
