namespace Evolith.Ums.Presentation.Middleware;

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Evolith.Ums.Infrastructure.Observability;

/// <summary>
/// Garantiza que toda petición tenga un identificador de rastreo de sesión, correlacionable
/// entre logs, trazas, flujos de negocio y traspasos a segundo plano.
///
/// El rastreo de sesión (SessionTrackingId) es un identificador de producto de UMS, ajeno al
/// estándar W3C: la correlación de traza (trace_id/span_id) la aporta <see cref="Activity.Current"/>
/// (traceparent), no este middleware.
/// </summary>
public sealed class SessionTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionTrackingMiddleware> _logger;

    public SessionTrackingMiddleware(
        RequestDelegate next,
        ILogger<SessionTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestContext requestContext)
    {
        var sessionTrackingId = GetOrAddSessionTrackingId(context);
        context.Response.Headers[ObservabilityHeaders.SessionTrackingId] = sessionTrackingId;

        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetBaggage(ObservabilityKeys.SessionTrackingId, sessionTrackingId);
            activity.SetTag(ObservabilityKeys.SessionTrackingId, sessionTrackingId);
        }

        requestContext.SetSessionTrackingId(sessionTrackingId);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["SessionTrackingId"] = sessionTrackingId,
        }))
        {
            await _next(context);
        }
    }

    private static string GetOrAddSessionTrackingId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ObservabilityHeaders.SessionTrackingId, out var existingId)
            && !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId!;
        }

        return Guid.NewGuid().ToString("N");
    }
}

public static class SessionTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionTracking(this IApplicationBuilder app)
        => app.UseMiddleware<SessionTrackingMiddleware>();
}
