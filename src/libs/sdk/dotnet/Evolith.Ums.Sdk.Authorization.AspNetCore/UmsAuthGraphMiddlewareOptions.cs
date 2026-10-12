namespace Evolith.Ums.Sdk.Authorization.AspNetCore;

/// <summary>Behavior knobs for <see cref="UmsAuthGraphMiddleware"/>.</summary>
public sealed class UmsAuthGraphMiddlewareOptions
{
    /// <summary>
    /// Secreto con el que UMS firma sus portadores en HS256.
    /// </summary>
    /// <remarks>
    /// <para>Ya no es lo habitual: desde ADR-0157 UMS firma en RS256 y publica su clave pública, así
    /// que lo normal es declarar <see cref="UmsBaseUrl"/> y no custodiar secreto ninguno. Sigue
    /// existiendo para los despliegues que todavía no han cruzado.</para>
    /// <para><b>Hay que declarar al menos uno</b>: este o <see cref="UmsBaseUrl"/>/<see cref="JwksUri"/>.
    /// Sin ninguno, el middleware descarta todo portador: sin material de verificación no puede
    /// distinguir uno legítimo de uno fabricado, que es el agujero de G-217.</para>
    /// </remarks>
    public string SigningSecret { get; set; } = string.Empty;

    /// <summary>
    /// Base de UMS. De ella sale su JWKS (<c>{base}/.well-known/jwks.json</c>), y declararla
    /// habilita la verificación RS256.
    /// </summary>
    /// <remarks>
    /// Declarar solo esto es lo deseable: el satélite verifica sin custodiar material de firma, y
    /// por tanto no puede emitir portadores aunque quisiera.
    /// </remarks>
    public string UmsBaseUrl { get; set; } = string.Empty;

    /// <summary>URI del JWKS, si no cuelga de <see cref="UmsBaseUrl"/> en la ruta estándar.</summary>
    public string JwksUri { get; set; } = string.Empty;

    /// <summary>Ajustes finos de la caché del JWKS (TTL, cadencia de refresco, tiempo de espera).</summary>
    public OpcionesDeJwks Jwks { get; set; } = new();

    /// <summary>JWT body claim that carries the serialized graph. Default: <c>"graph"</c>.</summary>
    public string JwtBodyClaim { get; set; } = "graph";

    /// <summary>When true, the middleware returns 401 + AUTH_201 when the bound graph is expired.</summary>
    public bool RejectExpiredGraphs { get; set; }

    /// <summary>
    /// When true, the middleware returns 401 + AUTH_204/AUTH_205 on missing or incompatible
    /// schemaVersion. When false (default), the graph is silently dropped and the request
    /// proceeds without authorization context — downstream code will see <c>Current = null</c>.
    /// </summary>
    public bool RejectIncompatibleGraphs { get; set; } = true;

    /// <summary>Resuelve la URI efectiva del JWKS, o cadena vacía si no se configuró ninguna.</summary>
    /// <returns>La URI del JWKS.</returns>
    internal string ResolverJwksUri()
    {
        if (!string.IsNullOrWhiteSpace(JwksUri)) return JwksUri;
        if (string.IsNullOrWhiteSpace(UmsBaseUrl)) return string.Empty;
        return $"{UmsBaseUrl.TrimEnd('/')}/.well-known/jwks.json";
    }
}
