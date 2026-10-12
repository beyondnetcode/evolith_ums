namespace Evolith.Ums.Presentation.Observability;

using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Enriquecedor de Serilog que añade <c>TraceId</c> y <c>SpanId</c> del contexto de traza
/// W3C (<see cref="Activity.Current"/>) a <b>toda</b> línea de log (ADR-0046).
///
/// Correlaciona de forma uniforme los logs (Loki) con las trazas (Tempo) sin depender del
/// sink de OTel: los sinks no-OTel (Console, Grafana Loki) transportan los mismos campos.
/// Solo emite cuando la <see cref="Activity"/> usa el formato de identificador W3C.
/// </summary>
public sealed class ActivityTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null || activity.IdFormat != ActivityIdFormat.W3C)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}
