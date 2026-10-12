
namespace Evolith.Ums.Application.Common.Aop;

/// <summary>
/// Marker interface that selects the Serilog-backed, observability-aware logger adapter
/// when decorating command handlers with <see cref="LoggerAspectAttribute"/>.
///
/// Unlike <see cref="IMelLogger"/> (which emits at Debug level via MEL),
/// <c>IUmsLogger</c> targets the Serilog pipeline and enriches every log entry with:
/// <list type="bullet">
///   <item><description><b>TenantId</b> — from <c>IUserContext</c> (multi-tenant dimension)</description></item>
///   <item><description><b>CorrelationId</b> — from <c>Activity.Current</c> baggage (set by <c>CorrelationIdMiddleware</c>)</description></item>
///   <item><description><b>SessionTrackingId</b> — from request execution context, propagated from the client/API request</description></item>
///   <item><description><b>TraceId / SpanId</b> — from <c>Activity.Current</c> (W3C trace context)</description></item>
///   <item><description><b>BoundedContext</b> — derived from the handler type namespace</description></item>
/// </list>
///
/// PII policy: argument <em>values</em> are never emitted — only parameter names and CLR types.
///
/// Usage on a command handler:
/// <code>
/// [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
/// public async Task&lt;Result&lt;T&gt;&gt; Handle(...) { ... }
/// </code>
/// </summary>
public interface IUmsLogger : ILogger;
