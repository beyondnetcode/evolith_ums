using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Application.Common.Interfaces;
using BeyondNetCode.Shell.Aop;
using BeyondNetCode.Shell.Aop.Aspects;

using MelILoggerFactory  = Microsoft.Extensions.Logging.ILoggerFactory;
using MelILogger         = Microsoft.Extensions.Logging.ILogger;

namespace Evolith.Ums.Infrastructure.Aop;

/// <summary>
/// Implementación de <see cref="IUmsLogger"/> respaldada por Serilog y consciente de la
/// observabilidad. Enriquece cada línea de log AOP con la envolvente de UMS:
/// <list type="bullet">
///   <item><description><b>TenantId</b>   — de <see cref="IUserContext"/> (dimensión multi-tenant)</description></item>
///   <item><description><b>TraceId / SpanId</b> — del contexto de traza W3C
///         (<see cref="Activity.Current"/> / <c>traceparent</c>), tras la unificación W3C (ADR-0046).
///         También los emite el enricher de Serilog en todas las líneas; aquí se hacen explícitos
///         para las líneas AOP.</description></item>
///   <item><description><b>CorrelationId</b> — es el <c>trace_id</c> W3C: la correlación deja de
///         generarse a mano (se retiró el shim vendorizado y el <c>X-Correlation-Id</c>).</description></item>
///   <item><description><b>SessionTrackingId</b> — de <see cref="IRequestContext"/> (rastreo de sesión de UMS)</description></item>
///   <item><description><b>BoundedContext</b> — inferido del namespace del handler</description></item>
/// </list>
///
/// Seguridad PII: nunca se emiten los <em>valores</em> de los argumentos, solo su nombre y tipo CLR.
/// </summary>
public sealed class UmsSerilogLogger(
    MelILoggerFactory loggerFactory,
    IUserContext userContext,
    IRequestContext requestContext) : IUmsLogger
{
    // ── Helpers ───────────────────────────────────────────────────────────────────────────

    private MelILogger Logger(IJoinPoint jp) =>
        loggerFactory.CreateLogger(jp.TargetType);

    /// <summary>Tenant del IUserContext scoped, o "system" fuera de una petición de usuario.</summary>
    private string TenantId() =>
        userContext.TenantId ?? "system";

    private (string CorrelationId, string SessionTrackingId, string TraceId, string SpanId) Correlation()
    {
        var activity = Activity.Current;
        var traceId = activity is { IdFormat: ActivityIdFormat.W3C } ? activity.TraceId.ToString() : string.Empty;
        var spanId  = activity is { IdFormat: ActivityIdFormat.W3C } ? activity.SpanId.ToString() : string.Empty;

        return (
            CorrelationId: requestContext.CorrelationId ?? traceId,
            SessionTrackingId: requestContext.SessionTrackingId ?? string.Empty,
            TraceId: traceId,
            SpanId: spanId);
    }

    private static string BoundedContext(Type targetType)
    {
        // Evolith.Ums.Application.<BoundedContext>.… → el contexto acotado es el 4.º segmento
        // (p. ej. Evolith.Ums.Application.Identity.Tenant.Commands → "Identity").
        var parts = targetType.Namespace?.Split('.') ?? Array.Empty<string>();
        if (parts.Length >= 4)
        {
            return parts[3];
        }

        return parts.Length >= 3 ? parts[2] : targetType.Name;
    }

    // ── Contrato ILogger ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void OnEntry(IJoinPoint joinPoint, Argument[] arguments, string requestId)
    {
        var log = Logger(joinPoint);
        if (!log.IsEnabled(LogLevel.Information)) return;

        var (correlationId, sessionTrackingId, traceId, spanId) = Correlation();
        var tenantId = TenantId();
        var bc       = BoundedContext(joinPoint.TargetType);

        // PII-safe: solo nombres + tipos CLR, nunca valores.
        var argSummary = arguments is { Length: > 0 }
            ? string.Join(", ", arguments.Select(a => $"{a.Name}:{a.Type}"))
            : string.Empty;

        if (string.IsNullOrEmpty(argSummary))
        {
            log.LogInformation(
                "→ {BoundedContext} {Handler}.{Method} | tenant={TenantId} cid={CorrelationId} sid={SessionTrackingId} trace={TraceId} span={SpanId}",
                bc, joinPoint.TargetType.Name, joinPoint.MethodInfo.Name,
                tenantId, correlationId, sessionTrackingId, traceId, spanId);
        }
        else
        {
            log.LogInformation(
                "→ {BoundedContext} {Handler}.{Method} params=[{Params}] | tenant={TenantId} cid={CorrelationId} sid={SessionTrackingId} trace={TraceId} span={SpanId}",
                bc, joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, argSummary,
                tenantId, correlationId, sessionTrackingId, traceId, spanId);
        }
    }

    /// <inheritdoc/>
    public void OnExit(IJoinPoint joinPoint, Return @return, string requestId, long duration)
        => LogExit(joinPoint, duration);

    /// <inheritdoc/>
    public void OnExit(IJoinPoint joinPoint, string requestId, long duration)
        => LogExit(joinPoint, duration);

    /// <inheritdoc/>
    public void OnExit(IJoinPoint joinPoint, Return @return, string requestId)
        => LogExit(joinPoint, duration: null);

    /// <inheritdoc/>
    public void OnExit(IJoinPoint joinPoint, string requestId)
        => LogExit(joinPoint, duration: null);

    private void LogExit(IJoinPoint joinPoint, long? duration)
    {
        var log = Logger(joinPoint);
        if (!log.IsEnabled(LogLevel.Information)) return;

        var (correlationId, sessionTrackingId, traceId, spanId) = Correlation();
        var tenantId = TenantId();

        if (duration is { } ms)
        {
            log.LogInformation(
                "← {BoundedContext} {Handler}.{Method} in {Duration}ms | tenant={TenantId} cid={CorrelationId} sid={SessionTrackingId} trace={TraceId} span={SpanId}",
                BoundedContext(joinPoint.TargetType),
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name,
                ms, tenantId, correlationId, sessionTrackingId, traceId, spanId);
        }
        else
        {
            log.LogInformation(
                "← {BoundedContext} {Handler}.{Method} | tenant={TenantId} cid={CorrelationId} sid={SessionTrackingId} trace={TraceId} span={SpanId}",
                BoundedContext(joinPoint.TargetType),
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name,
                tenantId, correlationId, sessionTrackingId, traceId, spanId);
        }
    }

    /// <inheritdoc/>
    public void OnException(IJoinPoint joinPoint, string requestId, Exception ex)
    {
        var log = Logger(joinPoint);
        var (correlationId, sessionTrackingId, traceId, spanId) = Correlation();
        var tenantId = TenantId();

        log.LogError(
            ex,
            "✗ {BoundedContext} {Handler}.{Method} threw {ExceptionType} | tenant={TenantId} cid={CorrelationId} sid={SessionTrackingId} trace={TraceId} span={SpanId}",
            BoundedContext(joinPoint.TargetType),
            joinPoint.TargetType.Name, joinPoint.MethodInfo.Name,
            ex.GetType().Name,
            tenantId, correlationId, sessionTrackingId, traceId, spanId);
    }
}
