namespace Evolith.Ums.Presentation.Endpoints.Configuration.AppConfiguration;

using System.Security.Claims;
using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Configuration.AppConfiguration.Commands;
using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;
using Evolith.Ums.Application.Configuration.AppConfiguration.Queries;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Presentation.Endpoints.Common;
using Evolith.Ums.Presentation.Extensions;

public static class AppConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapAppConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/app-configurations").WithTags("AppConfigurations");

        group.MapGet("/", async (
            [AsParameters] AppConfigurationPagedQueryParameters query,
            IMediator mediator,
            ITenantContext tenantContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Authorization: Internal admins can see all; tenant admins can only see their tenant's configs
            if (!tenantContext.IsInternalAdmin && query.Scope == "Global")
            {
                return Results.Json(new { error = "Global configuration is only accessible to internal administrators." }, statusCode: 403);
            }

            var result = await mediator.Send(new GetAllAppConfigurationsQuery(
                query.Page <= 0 ? 1 : query.Page,
                query.PageSize <= 0 ? 20 : query.PageSize,
                query.Search,
                string.IsNullOrWhiteSpace(query.Criteria) ? "code" : query.Criteria,
                string.IsNullOrWhiteSpace(query.Status) ? "all" : query.Status,
                string.IsNullOrWhiteSpace(query.SortBy) ? "code" : query.SortBy,
                string.IsNullOrWhiteSpace(query.SortOrder) ? "asc" : query.SortOrder,
                query.Scope,
                query.TenantId,
                query.SystemSuiteId,
                query.ModuleId), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllAppConfigurations")
        .Produces<PagedResult<AppConfigurationDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/", async (
            CreateAppConfigurationCommand command,
            IMediator mediator,
            ITenantContext tenantContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Authorization check for creating global configs
            var isGlobalScope = !command.TenantId.HasValue && !command.SystemSuiteId.HasValue && !command.ModuleId.HasValue;
            if (isGlobalScope && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "Only internal administrators can create global configurations." }, statusCode: 403);
            }

            // Authorization check for creating tenant-specific configs
            if (command.TenantId.HasValue && !tenantContext.IsInternalAdmin && command.TenantId != tenantContext.OrganizationId)
            {
                return Results.Json(new { error = "You do not have permission to create configurations for this tenant." }, statusCode: 403);
            }

            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/app-configurations/{r.AppConfigurationId}", context);
        })
        .WithName("CreateAppConfiguration")
        .Produces<CreateAppConfigurationResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // REC-10: Reads If-Match header for optimistic concurrency.
        // Clients should include the ETag received from the GET-by-ID response.
        // Omitting If-Match still works — EF Core tracks the version loaded during UpdateAsync.
        group.MapPut("/{appConfigurationId:guid}", async (
            Guid appConfigurationId,
            UpdateAppConfigurationCommand command,
            IMediator mediator,
            ITenantContext tenantContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Get the existing config to check scope
            var getResult = await mediator.Send(new GetAppConfigurationByIdQuery(appConfigurationId), ct);
            if (getResult.IsFailure)
            {
                return Results.NotFound();
            }

            var existingConfig = getResult.Value;

            // Authorization: Check if user can modify this config
            var isGlobalScope = existingConfig.Scope == "Global";
            var isOtherTenant = existingConfig.TenantId.HasValue &&
                                existingConfig.TenantId != tenantContext.OrganizationId;

            if (isGlobalScope && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "Only internal administrators can modify global configurations." }, statusCode: 403);
            }

            if (isOtherTenant && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "You do not have permission to modify this tenant's configurations." }, statusCode: 403);
            }

            var rowVersion = ETagHelper.DecodeIfMatch(context.Request.Headers.IfMatch);
            var result = await mediator.Send(
                command with { AppConfigurationId = appConfigurationId, RowVersion = rowVersion }, ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateAppConfiguration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{appConfigurationId:guid}/publish", async (
            Guid appConfigurationId,
            IMediator mediator,
            ITenantContext tenantContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Get the existing config to check scope
            var getResult = await mediator.Send(new GetAppConfigurationByIdQuery(appConfigurationId), ct);
            if (getResult.IsFailure)
            {
                return Results.NotFound();
            }

            var existingConfig = getResult.Value;

            // Authorization: Check if user can publish this config
            var isGlobalScope = existingConfig.Scope == "Global";
            var isOtherTenant = existingConfig.TenantId.HasValue &&
                                existingConfig.TenantId != tenantContext.OrganizationId;

            if (isGlobalScope && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "Only internal administrators can publish global configurations." }, statusCode: 403);
            }

            if (isOtherTenant && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "You do not have permission to publish this tenant's configurations." }, statusCode: 403);
            }

            var result = await mediator.Send(new PublishAppConfigurationCommand(appConfigurationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("PublishAppConfiguration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{appConfigurationId:guid}/archive", async (
            Guid appConfigurationId,
            IMediator mediator,
            ITenantContext tenantContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Get the existing config to check scope
            var getResult = await mediator.Send(new GetAppConfigurationByIdQuery(appConfigurationId), ct);
            if (getResult.IsFailure)
            {
                return Results.NotFound();
            }

            var existingConfig = getResult.Value;

            // Authorization: Check if user can archive this config
            var isGlobalScope = existingConfig.Scope == "Global";
            var isOtherTenant = existingConfig.TenantId.HasValue &&
                                existingConfig.TenantId != tenantContext.OrganizationId;

            if (isGlobalScope && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "Only internal administrators can archive global configurations." }, statusCode: 403);
            }

            if (isOtherTenant && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "You do not have permission to archive this tenant's configurations." }, statusCode: 403);
            }

            var result = await mediator.Send(new ArchiveAppConfigurationCommand(appConfigurationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ArchiveAppConfiguration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // G-143: borrado duro (DELETE). Replica la guarda de autorización por ámbito del archive.
        group.MapDelete("/{appConfigurationId:guid}", async (
            Guid appConfigurationId,
            IMediator mediator,
            ITenantContext tenantContext,
            HttpContext context,
            CancellationToken ct) =>
        {
            // Get the existing config to check scope
            var getResult = await mediator.Send(new GetAppConfigurationByIdQuery(appConfigurationId), ct);
            if (getResult.IsFailure)
            {
                return Results.NotFound();
            }

            var existingConfig = getResult.Value;

            // Authorization: Check if user can delete this config
            var isGlobalScope = existingConfig.Scope == "Global";
            var isOtherTenant = existingConfig.TenantId.HasValue &&
                                existingConfig.TenantId != tenantContext.OrganizationId;

            if (isGlobalScope && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "Only internal administrators can delete global configurations." }, statusCode: 403);
            }

            if (isOtherTenant && !tenantContext.IsInternalAdmin)
            {
                return Results.Json(new { error = "You do not have permission to delete this tenant's configurations." }, statusCode: 403);
            }

            var result = await mediator.Send(new DeleteAppConfigurationCommand(appConfigurationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeleteAppConfiguration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // DEFERRED: Rollback and version-comparison endpoints require a persisted
        // configuration history (audit table) — not yet implemented in the storage layer.
        return app;
    }
}
