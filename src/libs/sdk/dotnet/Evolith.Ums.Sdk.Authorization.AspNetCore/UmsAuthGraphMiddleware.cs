using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Contracts;


namespace Evolith.Ums.Sdk.Authorization.AspNetCore;

/// <summary>
/// Middleware that extracts the AuthorizationGraph from an incoming bearer JWT and stores it on
/// <c>HttpContext.Items["UmsAuthGraph"]</c> so the <see cref="HttpContextAuthGraphAccessor"/>
/// (and any downstream validator or AOP aspect) can read it.
///
/// The JWT is expected to be a 3-segment compact serialization. The payload section is base64url-
/// decoded as JSON and one of its top-level claims is treated as the serialized graph: either an
/// embedded object (when the claim is itself the graph) or a string containing the JSON to
/// re-parse (when the server packaged the graph as a string claim).
/// </summary>
public sealed class UmsAuthGraphMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private readonly RequestDelegate _next;
    private readonly IOptions<UmsAuthGraphMiddlewareOptions> _options;
    private readonly ILogger<UmsAuthGraphMiddleware>? _logger;

    /// <summary>
    /// Caché del JWKS del emisor. Se crea UNA vez, con el middleware, porque solo sirve de algo si
    /// sobrevive entre peticiones: creada por petición haría un GET al emisor por cada token.
    /// </summary>
    private readonly CacheDeJwks? _jwks;

    public UmsAuthGraphMiddleware(
        RequestDelegate next,
        IOptions<UmsAuthGraphMiddlewareOptions> options,
        ILogger<UmsAuthGraphMiddleware>? logger = null)
    {
        _next = next;
        _options = options;
        _logger = logger;

        ArgumentNullException.ThrowIfNull(options);
        var uriDelJwks = options.Value.ResolverJwksUri();
        if (uriDelJwks.Length > 0)
        {
            options.Value.Jwks.Uri = uriDelJwks;
            _jwks = new CacheDeJwks(options.Value.Jwks);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var opt = _options.Value;
        var graph = await ExtractGraphAsync(context, opt).ConfigureAwait(false);

        if (graph is not null)
        {
            if (string.IsNullOrWhiteSpace(graph.SchemaVersion))
            {
                if (opt.RejectIncompatibleGraphs)
                {
                    await Reject(context, "AUTH_204", "AuthorizationGraph payload does not declare a schemaVersion.").ConfigureAwait(false);
                    return;
                }
                _logger?.LogWarning("UmsAuthGraphMiddleware: incoming graph lacks schemaVersion — proceeding without binding.");
            }
            else if (!SchemaVersion.IsSupported(graph.SchemaVersion))
            {
                if (opt.RejectIncompatibleGraphs)
                {
                    await Reject(context, "AUTH_205",
                        $"Server emitted schemaVersion '{graph.SchemaVersion}' which is outside SDK compatibility.").ConfigureAwait(false);
                    return;
                }
                _logger?.LogWarning("UmsAuthGraphMiddleware: incompatible schemaVersion '{Version}' — proceeding without binding.", graph.SchemaVersion);
            }
            else if (opt.RejectExpiredGraphs && graph.ValidUntil <= DateTimeOffset.UtcNow)
            {
                await Reject(context, "AUTH_201", "AuthorizationGraph has expired.").ConfigureAwait(false);
                return;
            }
            else
            {
                context.Items[HttpContextAuthGraphAccessor.ItemsKey] = graph;
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private async Task<AuthorizationGraph?> ExtractGraphAsync(HttpContext context, UmsAuthGraphMiddlewareOptions opt)
    {
        string? token = ExtractBearerToken(context);
        if (token is null) return null;

        // Tres segmentos: cabecera, cuerpo y FIRMA. El tercero es el que hasta G-217 nadie miraba.
        var segments = token.Split('.');
        if (segments.Length != 3) return null;

        // La firma PRIMERO. Sin esto, todo lo que viene después —el grafo, la versión de esquema,
        // la caducidad— se comprueba sobre un contenido que cualquiera pudo escribir.
        if (!await FirmaValidaAsync(segments, opt, context.RequestAborted).ConfigureAwait(false))
        {
            _logger?.LogWarning(
                "UmsAuthGraphMiddleware: el portador no supera la verificación de firma; se descarta (G-217).");
            return null;
        }

        byte[] payload;
        try
        {
            payload = Base64UrlDecode(segments[1]);
        }
        catch
        {
            _logger?.LogWarning("UmsAuthGraphMiddleware: JWT payload is not valid base64url.");
            return null;
        }

        JsonElement payloadJson;
        try
        {
            payloadJson = JsonSerializer.Deserialize<JsonElement>(payload, JsonOptions);
        }
        catch
        {
            _logger?.LogWarning("UmsAuthGraphMiddleware: JWT payload is not valid JSON.");
            return null;
        }

        if (!payloadJson.TryGetProperty(opt.JwtBodyClaim, out var graphClaim))
            return null;

        try
        {
            return graphClaim.ValueKind switch
            {
                JsonValueKind.Object => graphClaim.Deserialize<AuthorizationGraph>(JsonOptions),
                JsonValueKind.String => JsonSerializer.Deserialize<AuthorizationGraph>(graphClaim.GetString()!, JsonOptions),
                _ => null
            };
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "UmsAuthGraphMiddleware: failed to deserialize '{Claim}' claim.", opt.JwtBodyClaim);
            return null;
        }
    }

    private static string? ExtractBearerToken(HttpContext context)
    {
        string? header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)) return null;
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[prefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    /// <summary>
    /// Comprueba que el portador lo firmó quien dice.
    /// </summary>
    /// <remarks>
    /// <para><b>El algoritmo del token no decide cómo se comprueba.</b> Solo elige entre las vías que
    /// el consumidor habilitó por configuración: RS256 si declaró el JWKS, HS256 si declaró el
    /// secreto. Aceptar lo que el token declare es la confusión de algoritmo clásica —basta con
    /// declarar <c>none</c> para que la firma deje de comprobarse—, y por eso <c>none</c> se
    /// rechaza explícitamente y cualquier otro algoritmo cae por no estar habilitado.</para>
    /// <para>Un satélite configurado solo para RS256 rechaza un token HS256 aunque el token insista:
    /// esa es la degradación que no puede ocurrir cuando las dos vías conviven.</para>
    /// </remarks>
    private async Task<bool> FirmaValidaAsync(
        string[] segments, UmsAuthGraphMiddlewareOptions opt, CancellationToken cancelacion)
    {
        var aceptaHs256 = !string.IsNullOrEmpty(opt.SigningSecret);
        var aceptaRs256 = _jwks is not null;

        // Sin material de verificación no se acepta nada. Es justamente la omisión de G-217.
        if (!aceptaHs256 && !aceptaRs256) return false;

        string alg;
        string kid;
        try
        {
            using var cabeceraJson = JsonDocument.Parse(Base64UrlDecode(segments[0]));
            alg = cabeceraJson.RootElement.TryGetProperty("alg", out var a) ? a.GetString() ?? string.Empty : string.Empty;
            kid = cabeceraJson.RootElement.TryGetProperty("kid", out var k) ? k.GetString() ?? string.Empty : string.Empty;
        }
        catch (Exception error) when (error is JsonException or FormatException)
        {
            return false;
        }

        // `none` se rechaza aquí, explícitamente. Caería igual por no estar habilitado, pero se
        // deja escrito: es LA manera de desactivar la firma sin tocarla, y debe verse contemplada.
        if (alg.Length == 0 || string.Equals(alg, "none", StringComparison.OrdinalIgnoreCase)) return false;

        var contenidoFirmado = Encoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");

        if (string.Equals(alg, "RS256", StringComparison.Ordinal))
        {
            return aceptaRs256
                && await Rs256ValidaAsync(kid, contenidoFirmado, segments[2], cancelacion).ConfigureAwait(false);
        }

        if (string.Equals(alg, "HS256", StringComparison.Ordinal))
        {
            return aceptaHs256 && Hs256Valida(contenidoFirmado, segments[2], opt.SigningSecret);
        }

        return false;
    }

    private async Task<bool> Rs256ValidaAsync(
        string kid, byte[] contenidoFirmado, string firmaB64, CancellationToken cancelacion)
    {
        // Sin `kid` no se verifica. La alternativa sería probar contra todas las claves publicadas,
        // y eso convierte cada rotación en una ventana donde la clave retirada sigue valiendo.
        if (kid.Length == 0) return false;

        var buscada = await _jwks!.ClaveParaAsync(kid, cancelacion).ConfigureAwait(false);
        if (buscada.Estado != EstadoDeClave.Ok || buscada.Clave is null) return false;

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportParameters(buscada.Clave.Value);
            return rsa.VerifyData(
                contenidoFirmado,
                Base64UrlDecode(firmaB64),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception error) when (error is CryptographicException or FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// HS256 en tiempo constante: comparar byte a byte con salida temprana filtra, por lo que tarda
    /// en fallar, cuántos bytes acertó quien lo intenta.
    /// </summary>
    private static bool Hs256Valida(byte[] contenidoFirmado, string firmaB64, string secreto)
    {
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));
            var esperada = hmac.ComputeHash(contenidoFirmado);
            var recibida = Base64UrlDecode(firmaB64);

            return CryptographicOperations.FixedTimeEquals(recibida, esperada);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var normalized = input.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2: normalized += "=="; break;
            case 3: normalized += "=";  break;
            case 1: throw new FormatException("Invalid base64url length.");
        }
        return Convert.FromBase64String(normalized);
    }

    private static async Task Reject(HttpContext context, string code, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new { code, message });
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(body)).ConfigureAwait(false);
    }
}
