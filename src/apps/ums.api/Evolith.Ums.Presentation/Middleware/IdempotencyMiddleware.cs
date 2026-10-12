namespace Evolith.Ums.Presentation.Middleware;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

/// <summary>
/// Request deduplication middleware (FIX-06 / RISK-05).
///
/// Reads the <c>Idempotency-Key</c> header on state-changing HTTP methods
/// (POST, PUT, PATCH). If the key has been seen before and the original request
/// completed successfully, the cached response is returned without re-executing
/// the handler — preventing duplicate aggregates, double charges, or partial-failure
/// retries from creating inconsistent state.
///
/// Behaviour:
/// - No header present → pass through unchanged (key is optional).
/// - New key           → mark as in-flight, execute pipeline, cache response, return.
/// - Completed key     → return cached response immediately (no handler invoked).
/// - In-flight key     → return 409 "request already in progress" (parallel duplicate).
///
/// Cache backend: <see cref="IDistributedCache"/>. Con Redis configurado, la deduplicación
/// es efectiva ENTRE pods; sin él, el registro cae en la implementación en memoria y solo
/// protege dentro del proceso. Antes usaba <c>IMemoryCache</c> siempre, así que un reintento
/// con la misma clave atendido por otro pod re-ejecutaba el comando: la garantía se anunciaba
/// pero no existía en cuanto había más de una réplica (G-169).
///
/// TTL: 24 hours (configurable via <see cref="IdempotencyOptions"/>).
/// Cached methods: POST, PUT, PATCH only (GET/DELETE are naturally idempotent).
/// </summary>
public sealed class IdempotencyMiddleware(
    RequestDelegate next,
    IDistributedCache cache,
    ILogger<IdempotencyMiddleware> logger)
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string InFlightSuffix       = ":inflight";

    private static readonly HashSet<string> CachedMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH" };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!CachedMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            await next(context);
            return;
        }

        var idempotencyKey = keyValues[0]!.Trim();
        var cacheKey       = $"idem:{idempotencyKey}";
        var inFlightKey    = cacheKey + InFlightSuffix;

        // ── Case 1: completed response already cached ──────────────────────────
        var cached = await cache.LeerAsync(cacheKey, context.RequestAborted);
        if (cached is not null)
        {
            logger.LogDebug(
                "IdempotencyMiddleware: returning cached response for key={Key} (status={Status}).",
                idempotencyKey, cached.StatusCode);

            context.Response.StatusCode  = cached.StatusCode;
            context.Response.ContentType = cached.ContentType;
            context.Response.Headers.Append("Idempotency-Key", idempotencyKey);
            context.Response.Headers.Append("X-Idempotency-Replayed", "true");

            if (cached.Body.Length > 0)
                await context.Response.Body.WriteAsync(cached.Body);

            return;
        }

        // ── Case 2: same key is in-flight (parallel duplicate request) ─────────
        if (await cache.GetAsync(inFlightKey, context.RequestAborted) is not null)
        {
            logger.LogWarning(
                "IdempotencyMiddleware: duplicate in-flight request for key={Key}.", idempotencyKey);

            context.Response.StatusCode  = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers.Append("Idempotency-Key", idempotencyKey);

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                title    = "Conflict",
                detail   = $"A request with Idempotency-Key '{idempotencyKey}' is already in progress. Wait for it to complete before retrying.",
                status   = 409,
            }));
            return;
        }

        // ── Case 3: new key — mark in-flight, execute, cache result ───────────
        var ttl = IdempotencyOptions.CacheTtl;
        var opciones = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await cache.SetAsync(inFlightKey, [1], opciones, context.RequestAborted);

        // Buffer the response so we can cache it
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            buffer.Seek(0, SeekOrigin.Begin);
            var body = buffer.ToArray();

            // Only cache successful responses (2xx)
            if (context.Response.StatusCode is >= 200 and <= 299)
            {
                var entry = new IdempotencyEntry(
                    context.Response.StatusCode,
                    context.Response.ContentType ?? "application/json",
                    body);

                await cache.SetAsync(
                    cacheKey,
                    JsonSerializer.SerializeToUtf8Bytes(entry),
                    opciones,
                    context.RequestAborted);

                logger.LogDebug(
                    "IdempotencyMiddleware: cached response for key={Key} (status={Status}, ttl={Ttl}h).",
                    idempotencyKey, context.Response.StatusCode, ttl.TotalHours);
            }

            // Write buffered response to original stream
            context.Response.Body = originalBody;
            context.Response.Headers.Append("Idempotency-Key", idempotencyKey);

            if (body.Length > 0)
                await originalBody.WriteAsync(body);
        }
        finally
        {
            // Always remove in-flight marker regardless of success/failure.
            // `CancellationToken.None`: si el cliente abortó, el token de la petición ya está
            // cancelado y la marca quedaría colgada hasta el TTL, bloqueando 24 h los reintentos
            // legítimos de esa misma clave.
            await cache.RemoveAsync(inFlightKey, CancellationToken.None);
            context.Response.Body = originalBody;
        }
    }
}

/// <summary>Cached idempotency response snapshot.</summary>
internal sealed record IdempotencyEntry(int StatusCode, string ContentType, byte[] Body);

/// <summary>Lee y deserializa una entrada cacheada; trata el dato corrupto como ausencia.</summary>
file static class IdempotencyCacheReader
{
    public static async Task<IdempotencyEntry?> LeerAsync(
        this IDistributedCache cache, string clave, CancellationToken ct)
    {
        var bytes = await cache.GetAsync(clave, ct);
        if (bytes is null || bytes.Length == 0) return null;

        try
        {
            return JsonSerializer.Deserialize<IdempotencyEntry>(bytes);
        }
        catch (JsonException)
        {
            // Formato antiguo o entrada corrupta: se re-ejecuta la petición en vez de fallar.
            return null;
        }
    }
}

/// <summary>Tunable constants for IdempotencyMiddleware.</summary>
public static class IdempotencyOptions
{
    /// <summary>How long a completed response is cached. Default: 24 hours.</summary>
    public static TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(24);
}

public static class IdempotencyMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="IdempotencyMiddleware"/> to the pipeline.
    /// Should be placed after <c>UseGlobalExceptionHandler</c> and before route execution.
    /// Requires <see cref="IMemoryCache"/> to be registered (<c>services.AddMemoryCache()</c>).
    /// </summary>
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app)
        => app.UseMiddleware<IdempotencyMiddleware>();
}
