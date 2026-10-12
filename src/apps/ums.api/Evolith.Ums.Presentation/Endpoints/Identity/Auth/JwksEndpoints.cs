namespace Evolith.Ums.Presentation.Endpoints.Identity.Auth;

using Evolith.Ums.Presentation.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// El material público con el que cualquiera verifica un portador de UMS.
/// </summary>
/// <remarks>
/// <para>
/// Es la mitad que faltaba de ADR-0157. Firmar en RS256 sin publicar la clave
/// pública no sirve de nada: ningún satélite podría verificar, y la única salida
/// sería repartir material de firma, que es exactamente lo que la decisión elimina.
/// </para>
/// <para>
/// <strong>Anónimo a propósito.</strong> Una clave pública es pública: exigir
/// credenciales para obtenerla crearía el problema del huevo y la gallina —haría
/// falta un token válido para conseguir con qué validar tokens—. Es lo que hacen
/// todos los proveedores de identidad, y por lo mismo.
/// </para>
/// </remarks>
public static class JwksEndpoints
{
    /// <summary>Mapea el JWKS y el documento de descubrimiento.</summary>
    /// <param name="app">Aplicación web.</param>
    public static void MapJwksEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        _ = app.MapGet("/.well-known/jwks.json", (MaterialDeFirma firma, HttpContext contexto) =>
            {
                // Cachear en el cliente, no en el servidor: el satélite guarda el JWKS y
                // solo vuelve cuando ve un `kid` que no conoce. Diez minutos es corto a
                // propósito —una rotación no debe tardar horas en propagarse— y suficiente
                // para que la verificación no dependa de la red en cada petición.
                contexto.Response.Headers.CacheControl = "public, max-age=600";

                return Results.Ok(new { keys = new[] { firma.ComoJwkPublico() } });
            })
            .WithName("Jwks")
            .WithTags("Discovery")
            .WithSummary("Claves públicas con las que se verifica un portador emitido por UMS")
            .AllowAnonymous();

        // Tres campos y ni uno más (ADR-0157 §4.1). UMS **no** declara con esto
        // conformidad como OpenID Provider, y no debe leerse así: el documento existe
        // porque los validadores de .NET y de Node resuelven las claves a través de él,
        // y publicarlo cuesta menos que cablear el `MetadataAddress` en cada satélite.
        _ = app.MapGet("/.well-known/openid-configuration", (
                MaterialDeFirma firma,
                IConfiguration configuracion,
                HttpContext contexto) =>
            {
                string emisor = configuracion["Jwt:Issuer"] ?? "ums-api";
                string baseUrl = $"{contexto.Request.Scheme}://{contexto.Request.Host}";

                contexto.Response.Headers.CacheControl = "public, max-age=600";

                return Results.Ok(new
                {
                    issuer = emisor,
                    jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                    id_token_signing_alg_values_supported = new[] { "RS256" },
                });
            })
            .WithName("OpenIdConfiguration")
            .WithTags("Discovery")
            .WithSummary("Descubrimiento mínimo: emisor, ubicación del JWKS y algoritmo de firma")
            .AllowAnonymous();
    }
}
