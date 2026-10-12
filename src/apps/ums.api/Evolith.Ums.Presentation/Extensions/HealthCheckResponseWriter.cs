namespace Evolith.Ums.Presentation.Extensions;

using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// REC-02: Serializa el resultado del health check como JSON estructurado.
/// Sigue el patrón habitual de health-check APIs (análogo a Spring Boot Actuator).
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        WriteIndented               = false,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status    = report.Status.ToString(),
            service   = "UMS API",
            timestamp = DateTimeOffset.UtcNow,
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name      = e.Key,
                status    = e.Value.Status.ToString(),
                duration  = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                tags      = e.Value.Tags,
            }),
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions));
    }
}
