namespace Evolith.Ums.Presentation.Middleware;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Infrastructure.Observability;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Abre y <b>cierra siempre</b> la transacción funcional de cada petición (ADR-0096 §2.3;
/// decisión UMS en ADR-UMS-085). Garantiza el <b>desenlace</b> por construcción: emite la
/// apertura al entrar y el desenlace en un <c>finally</c>, con el estado derivado del
/// resultado —incluida la ruta de fallo—. Así el invariante «transacción sin desenlace =
/// defecto alertable» no depende de la disciplina de cada handler.
///
/// Se ubica por <b>fuera</b> de <see cref="GlobalExceptionHandler"/> en el pipeline: cuando
/// un handler lanza, el manejador global convierte la excepción en respuesta (y adjunta el
/// localizador legible); este middleware, ya de vuelta, lee el código de estado final y
/// emite el desenlace correspondiente.
///
/// El localizador legible <c>TX-AAAA-NNNNNN</c> se acuña por adelantado en operaciones que
/// mutan estado (para que toda mutación porte su referencia) y de forma perezosa ante
/// cualquier fallo; las lecturas exitosas se identifican por su <c>traceId</c> W3C.
/// </summary>
public sealed class FunctionalTransactionMiddleware
{
    private readonly RequestDelegate _next;

    public FunctionalTransactionMiddleware(RequestDelegate next)
        => _next = next;

    public async Task InvokeAsync(HttpContext context, FunctionalTransaction transaction)
    {
        // Las sondas de salud no son transacciones funcionales.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var name = $"{context.Request.Method} {context.Request.Path}";
        transaction.Open(name, ResolveActor(context));

        if (IsMutating(context.Request.Method))
        {
            // No debe abortar la petición si la acuñación falla: se degrada a traceId.
            try
            {
                await transaction.GetOrMintLocatorAsync(CancellationToken.None);
            }
            catch
            {
                // El desenlace volverá a intentarlo si el resultado es un fallo.
            }
        }

        try
        {
            await _next(context);
        }
        finally
        {
            // El actor definitivo se conoce tras la autenticación (middleware interno).
            transaction.SetActor(ResolveActor(context));
            await transaction.CompleteAsync(
                DeriveState(context),
                context.Response.StatusCode,
                CancellationToken.None);
        }
    }

    private static TransactionState DeriveState(HttpContext context)
    {
        if (context.RequestAborted.IsCancellationRequested)
        {
            return TransactionState.Cancelled;
        }

        var statusCode = context.Response.StatusCode;
        return statusCode switch
        {
            StatusCodes.Status408RequestTimeout => TransactionState.TimedOut,
            >= 400 => TransactionState.Failed,
            _ => TransactionState.Completed,
        };
    }

    private static bool IsMutating(string method)
        => HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);

    private static string ResolveActor(HttpContext context)
    {
        var user = context.User;
        if (user?.Identity is { IsAuthenticated: true })
        {
            return user.Identity.Name
                ?? user.FindFirst("sub")?.Value
                ?? user.FindFirst("preferred_username")?.Value
                ?? "(autenticado)";
        }

        return "(anónimo)";
    }
}

public static class FunctionalTransactionMiddlewareExtensions
{
    public static IApplicationBuilder UseFunctionalTransaction(this IApplicationBuilder app)
        => app.UseMiddleware<FunctionalTransactionMiddleware>();
}
