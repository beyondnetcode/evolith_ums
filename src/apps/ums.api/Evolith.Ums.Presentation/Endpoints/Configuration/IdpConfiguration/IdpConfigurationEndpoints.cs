namespace Evolith.Ums.Presentation.Endpoints.Configuration.IdpConfiguration;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;
using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;

public static class IdpConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapIdpConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/idp-configurations").WithTags("IdpConfigurations");

        group.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] string? criteria,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid? systemSuiteId,
            [FromQuery] string? providerType,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllIdpConfigurationsQuery(
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : pageSize,
                search,
                string.IsNullOrWhiteSpace(criteria) ? "providerType" : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "resolutionPriority" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder,
                tenantId,
                systemSuiteId,
                providerType), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllIdpConfigurations")
        .Produces<PagedResult<IdpConfigurationDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateIdpConfigurationCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/idp-configurations/{r.IdpConfigurationId}", context);
        })
        .WithName("CreateIdpConfiguration")
        .Produces<CreateIdpConfigurationResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{idpConfigurationId:guid}", async (Guid idpConfigurationId, UpdateIdpConfigurationCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { IdpConfigurationId = idpConfigurationId }, ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateIdpConfiguration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{idpConfigurationId:guid}/activate", async (Guid idpConfigurationId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateIdpConfigurationCommand(idpConfigurationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ActivateIdpConfiguration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{idpConfigurationId:guid}/deactivate", async (Guid idpConfigurationId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateIdpConfigurationCommand(idpConfigurationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeactivateIdpConfiguration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{idpConfigurationId:guid}/archive", async (
            Guid idpConfigurationId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ArchiveIdpConfigurationCommand(idpConfigurationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ArchiveIdpConfiguration")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // DEFERRED: test-connection and fallback graph validation are operational endpoints
        // requiring IDP infrastructure access — outside the scope of pure CQRS command surface.
        return app;
    }
}
