namespace Evolith.Ums.Presentation.Extensions;

using Evolith.Ums.Presentation.Observability;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

/// <summary>
/// REC-14 / OBS-01: Configures Serilog as the application's structured-logging provider.
///
/// Output strategy:
/// - Development: human-readable coloured console (non-JSON, easy to read locally)
/// - Staging/Production: compact JSON to stdout + Grafana Loki push sink
///
/// Grafana Loki (OBS-01):
/// - Sink ships structured log events to Loki via HTTP push API.
/// - Low-cardinality labels: app, env (used for log stream indexing in Loki).
/// - High-cardinality fields (errorId, correlationId, userId) stay as log properties
///   so they are searchable via LogQL label filter expressions:
///     {app="ums-api"} |= "06c54bab-a4aa"   ← operator pastes the errorId from user report
/// - Exception stack traces are captured as the Serilog {Exception} token and
///   shipped to Loki — they NEVER appear in API response bodies (OBS-01 contract).
///
/// Enrichers always attached:
/// - MachineName, ThreadId — correlate logs from different pods/threads
/// - CorrelationId, SessionTrackingId — set by request middleware log scopes
/// - ErrorId — set by GlobalExceptionHandler / ResultExtensions log scopes
///
/// Configuration in appsettings.json:
/// <code>
/// "Observability": {
///   "Logging": {
///     "MinimumLevel": "Information",
///     "ConsoleFormat": "CompactJson",
///     "LokiEndpoint": "http://loki:3100",
///     "LokiAppLabel": "ums-api",
///     "LokiEnvLabel": "production"
///   }
/// }
/// </code>
/// </summary>
public static class LoggingExtensions
{
    public static LoggerConfiguration ConfigureUmsSerilog(
        this LoggerConfiguration loggerConfig,
        HostBuilderContext context)
    {
        var env = context.HostingEnvironment;
        var loggingSection = context.Configuration.GetSection("Observability:Logging");
        var consoleFormat = loggingSection["ConsoleFormat"] ?? (env.IsDevelopment() ? "Text" : "CompactJson");
        var minimumLevel  = loggingSection["MinimumLevel"]  ?? (env.IsDevelopment() ? "Debug" : "Information");
        var outputTemplate = loggingSection["OutputTemplate"]
            ?? "[{Timestamp:HH:mm:ss} {Level:u3}] trace={TraceId} span={SpanId} {SessionTrackingId} {ErrorId} {SourceContext} {Message:lj}{NewLine}{Exception}";

        loggerConfig
            .ReadFrom.Configuration(context.Configuration)     // honour appsettings Serilog section
            .Enrich.FromLogContext()                            // picks up BeginScope() key-values
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            // ADR-0046: TraceId/SpanId W3C (Activity.Current) en toda línea → correlación
            // uniforme de logs (Loki) con trazas (Tempo), también en sinks no-OTel.
            .Enrich.With<ActivityTraceEnricher>()
            // HARDENING-04: Mask PII fields before any sink sees them.
            .Enrich.With<PiiSanitizerEnricher>()
            .Destructure.With<PiiMaskingPolicy>()
            .MinimumLevel.Is(ParseLevel(minimumLevel))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning);

        // ── Console sink ───────────────────────────────────────────────────────
        if (string.Equals(consoleFormat, "CompactJson", StringComparison.OrdinalIgnoreCase))
            loggerConfig.WriteTo.Console(new CompactJsonFormatter());
        else
            loggerConfig.WriteTo.Console(outputTemplate: outputTemplate);

        // ── Grafana Loki sink (OBS-01) ─────────────────────────────────────────
        // Active when an endpoint is configured. In development, leave LokiEndpoint
        // blank to skip; in production, point to the Loki push API URL.
        var lokiEndpoint = loggingSection["LokiEndpoint"];
        if (!string.IsNullOrWhiteSpace(lokiEndpoint))
        {
            var appLabel = loggingSection["LokiAppLabel"] ?? "ums-api";
            var envLabel = loggingSection["LokiEnvLabel"] ?? env.EnvironmentName.ToLowerInvariant();

            // Labels are low-cardinality identifiers used for Loki stream selection.
            // High-cardinality values (errorId, userId, traceId) are log properties,
            // not labels, so they don't fragment the Loki index.
            var labels = new LokiLabel[]
            {
                new() { Key = "app", Value = appLabel },
                new() { Key = "env", Value = envLabel },
            };

            loggerConfig.WriteTo.GrafanaLoki(
                uri: lokiEndpoint,
                labels: labels,
                // Properties to promote to Loki structured metadata (queryable via LogQL).
                // These remain as log-line JSON values — never response-body fields.
                propertiesAsLabels: ["level"],
                batchPostingLimit: 100,
                period: TimeSpan.FromSeconds(2));
        }

        return loggerConfig;
    }

    private static LogEventLevel ParseLevel(string value)
        => Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var parsed)
            ? parsed
            : LogEventLevel.Information;
}
