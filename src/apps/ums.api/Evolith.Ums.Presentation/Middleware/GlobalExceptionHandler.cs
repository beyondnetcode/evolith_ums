namespace Evolith.Ums.Presentation.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Evolith.Ums.Globalization.Access;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Presentation.Extensions;

/// <summary>
/// Global exception handler middleware.
///
/// SECURITY CONTRACT (OBS-01):
/// - Response body NEVER contains stack traces, exception types, or internal identifiers.
/// - The <c>errorId</c> is the only handle exposed to the caller; all technical details
///   (exception type, stack trace, correlation IDs) are emitted to the structured log
///   (Serilog → Grafana Loki) keyed on that same <c>errorId</c>.
/// - Operators look up <c>errorId</c> in Loki to retrieve the full diagnostic context.
/// </summary>
public sealed class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        RequestDelegate next,
        ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var errorId = UserFacingErrorContext.GetOrCreateErrorId(context);
        var statusCode = GetStatusCode(exception);

        // ADR-0096 §2.2 / ADR-UMS-084: acuña el localizador legible TX-AAAA-NNNNNN y muéstralo
        // al usuario en el fallo. Es lo que pega en el ticket de soporte. Nunca debe convertir
        // el manejo del error en un fallo secundario.
        var transactionLocator = await TryMintLocatorAsync(context);

        var problemDetails = CreateProblemDetails(context, exception, errorId, transactionLocator, statusCode);

        // Full diagnostic context goes to the log — never to the response body.
        LogException(exception, context, errorId);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, UmsProblemDetailsJsonOptions.Instance));
    }

    /// <summary>
    /// Acuña el localizador legible de la transacción funcional (ADR-UMS-084) sin propagar
    /// fallos: si la acuñación fallara, el error original sigue devolviéndose con su errorId.
    /// </summary>
    private static async Task<string?> TryMintLocatorAsync(HttpContext context)
    {
        try
        {
            var transaction = context.RequestServices
                .GetService(typeof(Application.Common.Interfaces.IFunctionalTransaction))
                as Application.Common.Interfaces.IFunctionalTransaction;

            if (transaction is null)
            {
                return null;
            }

            return await transaction.GetOrMintLocatorAsync(context.RequestAborted);
        }
        catch
        {
            return null;
        }
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext context,
        Exception exception,
        string errorId,
        string? transactionLocator,
        int statusCode)
    {
        var problemDetails = new ProblemDetails
        {
            Title   = GetErrorTitle(exception),
            Detail  = GetErrorDetail(exception),   // localized, user-friendly — no internal info
            Status  = statusCode,
            Type    = $"https://httpstatuses.io/{statusCode}",
            Instance = $"{context.Request.Path}{context.Request.QueryString}",
            Extensions =
            {
                // errorId is the only handle the user gives to support.
                // Support queries Grafana Loki: {app="ums-api"} |= "<errorId>"
                ["errorId"]   = errorId,
                ["timestamp"] = DateTimeOffset.UtcNow,
            },
        };

        // Localizador legible TX-AAAA-NNNNNN: la referencia comunicable por voz/chat que el
        // usuario final entrega a soporte (ADR-0096 §2.2). Convive con el errorId técnico.
        if (!string.IsNullOrWhiteSpace(transactionLocator))
        {
            problemDetails.Extensions["transactionId"] = transactionLocator;
            problemDetails.Extensions["supportReference"] = transactionLocator;
        }

        return problemDetails;
    }

    private static string GetErrorTitle(Exception exception) => exception switch
    {
        ConcurrencyConflictException                          => "Conflict",
        Evolith.Ums.Application.Common.Aop.AccesoDenegadoException => "Forbidden",
        UnauthorizedAccessException                           => "Unauthorized",
        System.Collections.Generic.KeyNotFoundException       => "Not Found",
        // Parámetros de query/ruta ausentes o mal tipados: ASP.NET responde 400.
        // El título debe reflejar el 400 y NO "Internal Server Error" (p.ej. GET
        // /system-suites sin `page`/`pageSize`).
        Microsoft.AspNetCore.Http.BadHttpRequestException     => "Bad Request",
        InvalidOperationException                             => "Invalid Operation",
        ArgumentException                                     => "Bad Request",
        _                                                     => "Internal Server Error",
    };

    private static string GetErrorDetail(Exception exception) => exception switch
    {
        ConcurrencyConflictException                    => StringLocalizer.T("error.operation.conflict"),
        Evolith.Ums.Application.Common.Aop.AccesoDenegadoException => StringLocalizer.T("error.authorization.forbidden"),
        UnauthorizedAccessException                     => StringLocalizer.T("error.authentication.required"),
        System.Collections.Generic.KeyNotFoundException => StringLocalizer.T("error.resource.not_found"),
        Microsoft.AspNetCore.Http.BadHttpRequestException => StringLocalizer.T("error.request.invalid"),
        ArgumentException                               => StringLocalizer.T("error.request.invalid"),
        _                                               => StringLocalizer.T("error.unexpected"),
    };

    private static int GetStatusCode(Exception exception) => exception switch
    {
        ConcurrencyConflictException                          => StatusCodes.Status409Conflict,
        // Identificado pero sin permiso: 403. Ver `AccesoDenegadoException`.
        Evolith.Ums.Application.Common.Aop.AccesoDenegadoException => StatusCodes.Status403Forbidden,
        UnauthorizedAccessException                           => StatusCodes.Status401Unauthorized,
        System.Collections.Generic.KeyNotFoundException       => StatusCodes.Status404NotFound,
        // Parámetros de query/ruta ausentes o mal tipados: ASP.NET lanza esto con
        // su propio StatusCode (400). Sin este caso caía a 500 (p.ej. GET
        // /system-suites sin `page`).
        Microsoft.AspNetCore.Http.BadHttpRequestException badRequest => badRequest.StatusCode,
        InvalidOperationException                             => StatusCodes.Status400BadRequest,
        ArgumentException                                     => StatusCodes.Status400BadRequest,
        _                                                     => StatusCodes.Status500InternalServerError,
    };

    /// <summary>
    /// Logs the full exception with all diagnostic context.
    /// The <paramref name="errorId"/> is the correlation key between this log entry
    /// and what the user sees on screen — operators use it to look up the full details
    /// in Grafana Loki without the user needing to understand technical internals.
    /// </summary>
    private void LogException(Exception exception, HttpContext context, string errorId)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["ErrorId"]         = errorId,
            ["CorrelationId"]   = context.TraceIdentifier,
            ["RequestPath"]     = $"{context.Request.Method} {context.Request.Path}",
            ["ExceptionType"]   = exception.GetType().FullName ?? exception.GetType().Name,
        }))
        {
            _logger.LogError(
                exception,
                "Unhandled exception [{ExceptionType}] on {RequestMethod} {RequestPath}. " +
                "ErrorId: {ErrorId} — look up in Loki to see full stack trace.",
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                errorId);
        }
    }
}

public static class GlobalExceptionHandlerExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionHandler>();
}
