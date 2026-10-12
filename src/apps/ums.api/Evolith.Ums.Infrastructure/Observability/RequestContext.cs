namespace Evolith.Ums.Infrastructure.Observability;

using System.Diagnostics;

/// <summary>
/// Contexto de ejecución de la petición (scoped). Expone a la capa de aplicación los
/// datos de correlación W3C y el identificador de rastreo de sesión de UMS.
///
/// Reemplaza a <c>RequestContextAccessor</c> + <c>ExecutionContextSnapshot</c> +
/// <c>IExecutionContextAccessor</c>, andamiaje que se vendorizó bajo el namespace de
/// los shells (gap G-015) y se retiró al unificar la correlación en W3C TraceContext
/// (ADR-0046):
/// <list type="bullet">
///   <item><description><b>TraceId / SpanId</b> — derivados de <see cref="Activity.Current"/>
///         (contexto de traza W3C propagado por <c>traceparent</c>). Ya no se calculan a mano.</description></item>
///   <item><description><b>CorrelationId</b> — es el <c>trace_id</c> W3C: la correlación deja de
///         generarse como un GUID propio (<c>X-Correlation-Id</c>).</description></item>
///   <item><description><b>SessionTrackingId</b> — identificador de rastreo de sesión de negocio
///         de UMS, ajeno al estándar W3C; lo fija <c>SessionTrackingMiddleware</c>.</description></item>
///   <item><description><b>ClientTimezone</b> — zona horaria IANA del cliente (ADR-0076 D2);
///         la fija <c>CultureMiddleware</c>.</description></item>
/// </list>
/// </summary>
public sealed class RequestContext : IRequestContext
{
    private string? _sessionTrackingId;
    private string? _clientTimezone;

    public string? SessionTrackingId => _sessionTrackingId;

    /// <summary>Unificación W3C (ADR-0046): la correlación es el <c>trace_id</c> de <c>traceparent</c>.</summary>
    public string? CorrelationId => TraceId;

    public string? TraceId
    {
        get
        {
            var activity = Activity.Current;
            return activity is { IdFormat: ActivityIdFormat.W3C } ? activity.TraceId.ToString() : null;
        }
    }

    public string? SpanId
    {
        get
        {
            var activity = Activity.Current;
            return activity is { IdFormat: ActivityIdFormat.W3C } ? activity.SpanId.ToString() : null;
        }
    }

    public string? ClientTimezone => _clientTimezone;

    public void SetSessionTrackingId(string? sessionTrackingId)
        => _sessionTrackingId = string.IsNullOrWhiteSpace(sessionTrackingId) ? null : sessionTrackingId;

    public void SetClientTimezone(string? timezone)
        => _clientTimezone = string.IsNullOrWhiteSpace(timezone) ? null : timezone;
}
