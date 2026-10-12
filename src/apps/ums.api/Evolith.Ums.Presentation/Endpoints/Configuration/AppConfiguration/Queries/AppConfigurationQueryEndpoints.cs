namespace Evolith.Ums.Presentation.Endpoints.Configuration.AppConfiguration.Queries;

using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;
using Evolith.Ums.Application.Configuration.AppConfiguration.Queries;
using Evolith.Ums.Infrastructure.Persistence;
using Evolith.Ums.Presentation.Extensions;
using Microsoft.EntityFrameworkCore;

public static class AppConfigurationQueryEndpoints
{
    public static IEndpointRouteBuilder MapAppConfigurationQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/app-configurations").WithTags("AppConfigurations - Queries");

        // Hierarchy-aware resolve: returns the most specific effective value for the given scope
        // context (Module → Suite → Tenant → Global, BR-1). Encrypted values are redacted for
        // non-admin callers.
        group.MapGet("/resolve", async (
            [FromQuery] string  code,
            [FromQuery] Guid?   tenantId,
            [FromQuery] Guid?   suiteId,
            [FromQuery] Guid?   moduleId,
            IMediator           mediator,
            ITenantContext      tenantContext,
            HttpContext         context,
            CancellationToken   ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { error = "code is required." });

            if (!tenantContext.IsInternalAdmin && tenantId.HasValue && tenantId != tenantContext.OrganizationId)
                return Results.Json(new { error = "You may only resolve configurations for your own tenant." }, statusCode: 403);

            var result = await mediator.Send(
                new ResolveAppConfigurationQuery(code, tenantId, suiteId, moduleId), ct);

            return result.ToOk(context);
        })
        .WithName("ResolveAppConfiguration")
        .Produces<ResolvedAppConfigurationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // REC-10: GET-by-ID sets ETag header from SQL RowVersion for optimistic-locking support.
        // Clients should send the ETag value as an If-Match header on subsequent PUT requests.
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IMediator mediator,
            [FromServices] IServiceProvider services,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAppConfigurationByIdQuery(id), ct);

            var dbContext = services.GetService<UmsPlatformDbContext>();

            if (result.IsSuccess && dbContext is not null)
            {
                // Attach ETag from RowVersion — read-side query straight from DB context
                var rv = await dbContext.AppConfigurations
                    .Where(x => x.Id == id)
                    .Select(x => x.RowVersion)
                    .FirstOrDefaultAsync(ct);

                var etag = ETagHelper.Encode(rv);
                if (etag is not null)
                    context.Response.Headers.ETag = etag;
            }

            return result.ToOk(context);
        })
        .WithName("GetAppConfigurationById")
        .Produces<AppConfigurationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
