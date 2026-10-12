namespace Evolith.Ums.Presentation.Extensions;

#pragma warning disable S125

using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

/// <summary>
/// REC-06: Configura OpenTelemetry para trazabilidad distribuida y métricas.
///
/// Activa:
/// - Tracing: ASP.NET Core, HttpClient, EF Core (via source "Microsoft.EntityFrameworkCore")
/// - Metrics: ASP.NET Core, HttpClient, Runtime (.NET GC/threadpool)
/// - Exporter: OTLP (compatible con Jaeger, Tempo, Datadog, Azure Monitor, etc.)
///
/// Configuración en appsettings.json:
/// <code>
/// "OpenTelemetry": {
///   "Endpoint": "http://otel-collector:4317",
///   "ServiceVersion": "1.0.0"
/// }
/// </code>
/// </summary>
public static class ObservabilityExtensions
{
    private const string ServiceName = "ums-api";

    public static IServiceCollection AddUmsObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var endpoint = configuration["Observability:Tracing:OtlpEndpoint"]
            ?? configuration["OpenTelemetry:Endpoint"];
        var version  = configuration["Observability:Tracing:ServiceVersion"]
            ?? configuration["OpenTelemetry:ServiceVersion"]
            ?? "1.0.0";

        // ADR-0096 §2.1: el entorno se declara con la semconv OTel `deployment.environment.name`.
        //
        // En MINUSCULA, y no por gusto: `LoggingExtensions` ya normaliza asi la etiqueta `env` que
        // viaja a Loki (`env.EnvironmentName.ToLowerInvariant()`). Sin esta linea, el mismo
        // despliegue se declaraba `uat` en sus logs y `UAT` en sus metricas y trazas --medido el
        // 2026-09-01 en SRVTMS-- y correlacionar las tres senales por entorno no casaba, porque
        // ni Loki ni Prometheus comparan etiquetas sin distinguir mayusculas.
        var environmentName = (configuration["Observability:Environment"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "unknown").ToLowerInvariant();

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: ServiceName,
                serviceVersion: version,
                autoGenerateServiceInstanceId: true)
            .AddAttributes(new[]
            {
                new KeyValuePair<string, object>("deployment.environment.name", environmentName),
            })
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector();

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        // Exclude health check noise from traces
                        opts.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/health");
                        opts.EnrichWithHttpRequest = (activity, request) =>
                        {
                            if (request.Headers.TryGetValue(ObservabilityHeaders.SessionTrackingId, out var sessionTrackingId)
                                && !string.IsNullOrWhiteSpace(sessionTrackingId))
                            {
                                activity.SetTag(ObservabilityKeys.SessionTrackingId, sessionTrackingId.ToString());
                            }
                        };
                    })
                    .AddHttpClientInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        opts.EnrichWithHttpRequestMessage = (activity, _) =>
                        {
                            var sessionTrackingId = Activity.Current?.GetBaggageItem(ObservabilityKeys.SessionTrackingId);
                            if (!string.IsNullOrWhiteSpace(sessionTrackingId))
                            {
                                activity.SetTag(ObservabilityKeys.SessionTrackingId, sessionTrackingId);
                            }
                        };
                    })
                    // EF Core SQL queries traced via activity source
                    .AddSource("Microsoft.EntityFrameworkCore")
                    // ADR-UMS-098: los aspectos AOP de los shells emiten spans con un
                    // ActivitySource nativo de la BCL (sin SDK de OTel en la librería);
                    // registrarlo aquí incorpora esos spans al TracerProvider.
                    .AddSource("BeyondNetCode.Shell.Aop");

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    tracing.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(endpoint);
                    });
                }
                // Without an endpoint configured, traces are collected in-process
                // (useful for health checks and local profiling) but not exported.
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    // SessionTrackingId is intentionally not emitted as a metric dimension.
                    // It is too high-cardinality for general-purpose metrics backends.
                    // Custom UMS meters (reserved for future instrumentation)
                    .AddMeter("UMS.Application")
                    .AddMeter("UMS.Infrastructure");

                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    metrics.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(endpoint);
                    });
                }
            });

        return services;
    }
}
