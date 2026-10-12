using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Evolith.Ums.Application.Common.Aop;
using BeyondNetCode.Shell.Aop;
using BeyondNetCode.Shell.Aop.Aspects;

using AopILogger = BeyondNetCode.Shell.Aop.Aspects.ILogger;
using MelILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Evolith.Ums.Infrastructure.Aop;

/// <summary>
/// Microsoft.Extensions.Logging adapter for <see cref="BeyondNetCode.Shell.Aop.Aspects.ILogger"/>.
///
/// Registered in DI as a keyed service with key <c>typeof(<see cref="IMelLogger"/>)</c> so that
/// handlers annotated with <c>[LoggerAspect(Type = typeof(IMelLogger), ...)]</c> resolve this
/// implementation without any direct Infrastructure dependency in the Application layer.
///
/// PII safety: argument <em>values</em> are never emitted — only parameter names and CLR types
/// appear in log output.  Use <see cref="BeyondNetCode.Shell.Aop.Aspects.Logger.Serilog.SerilogLogger"/>
/// with structured destructuring if value capture is explicitly required and a PII review has
/// been performed.
/// </summary>
public sealed class MelLogger(MelILoggerFactory loggerFactory) : IMelLogger
{
    private Microsoft.Extensions.Logging.ILogger Logger(IJoinPoint jp) =>
        loggerFactory.CreateLogger(jp.TargetType);

    // ── Entry ─────────────────────────────────────────────────────────────────────────

    public void OnEntry(IJoinPoint joinPoint, Argument[] arguments, string requestId)
    {
        var log = Logger(joinPoint);
        if (!log.IsEnabled(LogLevel.Debug)) return;

        if (string.IsNullOrEmpty(requestId))
            log.LogDebug("Enter {Class}.{Method}",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name);
        else
            log.LogDebug("Enter {Class}.{Method} [{RequestId}]",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, requestId);

        // PII-safe: only log parameter names and CLR types, never values.
        if (arguments is { Length: > 0 })
        {
            var argSummary = string.Join(", ", arguments.Select(a => $"{a.Name}:{a.Type}"));
            log.LogDebug("  params [{Args}]", argSummary);
        }
    }

    // ── Exit (four overloads required by ILogger contract) ───────────────────────────

    public void OnExit(IJoinPoint joinPoint, Return @return, string requestId, long duration)
    {
        var log = Logger(joinPoint);
        if (string.IsNullOrEmpty(requestId))
            log.LogDebug("Exit {Class}.{Method} in {Duration}ms",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, duration);
        else
            log.LogDebug("Exit {Class}.{Method} [{RequestId}] in {Duration}ms",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, requestId, duration);
    }

    public void OnExit(IJoinPoint joinPoint, string requestId, long duration)
    {
        var log = Logger(joinPoint);
        if (string.IsNullOrEmpty(requestId))
            log.LogDebug("Exit {Class}.{Method} in {Duration}ms",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, duration);
        else
            log.LogDebug("Exit {Class}.{Method} [{RequestId}] in {Duration}ms",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, requestId, duration);
    }

    public void OnExit(IJoinPoint joinPoint, Return @return, string requestId)
    {
        var log = Logger(joinPoint);
        if (string.IsNullOrEmpty(requestId))
            log.LogDebug("Exit {Class}.{Method}",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name);
        else
            log.LogDebug("Exit {Class}.{Method} [{RequestId}]",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, requestId);
    }

    public void OnExit(IJoinPoint joinPoint, string requestId)
    {
        var log = Logger(joinPoint);
        if (string.IsNullOrEmpty(requestId))
            log.LogDebug("Exit {Class}.{Method}",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name);
        else
            log.LogDebug("Exit {Class}.{Method} [{RequestId}]",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, requestId);
    }

    // ── Exception ─────────────────────────────────────────────────────────────────────

    public void OnException(IJoinPoint joinPoint, string requestId, Exception ex)
    {
        var log = Logger(joinPoint);
        if (string.IsNullOrEmpty(requestId))
            log.LogError(ex, "Exception in {Class}.{Method}",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name);
        else
            log.LogError(ex, "Exception in {Class}.{Method} [{RequestId}]",
                joinPoint.TargetType.Name, joinPoint.MethodInfo.Name, requestId);
    }
}
