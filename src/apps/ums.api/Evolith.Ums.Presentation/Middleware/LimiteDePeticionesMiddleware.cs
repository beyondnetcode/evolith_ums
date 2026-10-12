namespace Evolith.Ums.Presentation.Middleware;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// G-248 — el cupo de peticiones, contado en un sitio que todas las réplicas comparten y con la
/// identidad de quien llama ya resuelta.
///
/// <para>Sustituye al <c>PartitionedRateLimiter</c> de ASP.NET, que fallaba por dos motivos a la
/// vez. Contaba <b>en proceso</b>, así que con N réplicas el cupo efectivo era N veces el
/// declarado. Y se ejecutaba <b>antes</b> de <c>UseAuthentication</c>, de modo que
/// <c>HttpContext.User</c> venía vacío y la clave de reparto caía siempre a la IP: dos usuarios
/// distintos compartían cupo. Medido el 2026-08-04 con el cupo en 6 — tres peticiones de una cuenta
/// y tres de otra bastaban para que ambas recibieran 429.</para>
///
/// <para>Detrás de un Ingress eso era peor de lo que parece: la IP que ve el proceso es la del
/// controlador, no la del cliente, así que el tráfico anónimo entero caía en un único cubo y un solo
/// abusador dejaba fuera a los demás. Por eso la IP se lee de <c>X-Forwarded-For</c> cuando viene,
/// que es lo que el Ingress rellena.</para>
///
/// <para>Va DESPUÉS de <c>UseAuthentication</c> y <c>UseAuthorization</c>, y antes de los
/// endpoints: es el único punto donde se sabe a quién se está limitando.</para>
/// </summary>
public sealed class LimiteDePeticionesMiddleware(RequestDelegate next, int cupo, TimeSpan ventana)
{
    public async Task InvokeAsync(HttpContext context, ILimitadorDePeticiones limitador)
    {
        // Las sondas quedan fuera: kubelet las llama sin credencial y desde la IP del nodo, así que
        // consumirían el cupo anónimo y podrían llegar a limitarse a sí mismas. Un limitador que
        // provoca reinicios en cadena hace más daño que el abuso del que protege.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var clave = ResolverClave(context);
        var resultado = await limitador.RegistrarAsync(clave, cupo, ventana, context.RequestAborted);

        if (resultado.Permitida)
        {
            await next(context);
            return;
        }

        var segundos = Math.Max(1, (int)Math.Ceiling(resultado.EsperaSugerida.TotalSeconds));
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = segundos.ToString();
        context.Response.ContentType = "application/problem+json";

        // Mismo cuerpo que emitía el limitador anterior: quien ya trate un 429 no debe notar el
        // cambio de implementación.
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Please try again later.",
            Status = StatusCodes.Status429TooManyRequests,
            Type = "https://httpstatuses.io/429",
            Extensions = { ["retryAfter"] = segundos },
        });
    }

    /// <summary>
    /// A quién se le cuenta esta petición. De lo más específico a lo más general: si se sabe el
    /// inquilino y el usuario, el cupo es suyo; si no, de quien se pueda identificar.
    /// </summary>
    private static string ResolverClave(HttpContext ctx)
    {
        var tenantId = ctx.User.FindFirst("tenant_id")?.Value
                    ?? ctx.User.FindFirst("org_id")?.Value;

        var sub = ctx.User.FindFirst("sub")?.Value
               ?? ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(sub))
            return $"tenant:{tenantId}:user:{sub}";

        if (!string.IsNullOrEmpty(sub))
            return $"user:{sub}";

        var apiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
            return $"apikey:{apiKey}";

        return $"ip:{IpDelCliente(ctx)}";
    }

    /// <summary>
    /// La IP del cliente, no la del Ingress.
    ///
    /// <para>Se toma la PRIMERA de <c>X-Forwarded-For</c>, que es la del cliente original; las
    /// siguientes son los saltos intermedios. La cabecera la puede falsificar quien llame
    /// directamente al pod, y por eso solo gobierna el cupo anónimo: quien se autentica se cuenta
    /// por su identidad, que no se puede inventar sin credencial.</para>
    /// </summary>
    private static string IpDelCliente(HttpContext ctx)
    {
        var reenviada = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(reenviada))
        {
            var primera = reenviada.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(primera)) return primera;
        }

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    }
}

public static class LimiteDePeticionesMiddlewareExtensions
{
    /// <summary>
    /// Registra el límite de peticiones. Debe ir DESPUÉS de <c>UseAuthentication</c>: antes,
    /// <c>HttpContext.User</c> viene vacío y el cupo de una IP lo comparten cuantos la usen (G-248).
    /// </summary>
    public static IApplicationBuilder UseLimiteDePeticiones(
        this IApplicationBuilder app, IConfiguration configuration)
    {
        var seccion = configuration.GetSection("ApiSettings:RateLimiting");
        var cupo = seccion.GetValue<int>("PermitLimit", 100);
        var ventana = TimeSpan.FromMinutes(seccion.GetValue<int>("WindowMinutes", 1));

        return app.UseMiddleware<LimiteDePeticionesMiddleware>(cupo, ventana);
    }
}
