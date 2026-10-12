namespace Evolith.Ums.Presentation.Endpoints.Configuration.IdpConfiguration;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;
using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;
using Evolith.Ums.Presentation.Endpoints.Common;

public static class IdpConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapIdpConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/idp-configurations").WithTags("IdpConfigurations");

        group.MapGet("/", async (
            [AsParameters] IdpConfigurationPagedQueryParameters query,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllIdpConfigurationsQuery(
                query.Page <= 0 ? 1 : query.Page,
                query.PageSize <= 0 ? 20 : query.PageSize,
                query.Search,
                string.IsNullOrWhiteSpace(query.Criteria) ? "providerType" : query.Criteria,
                string.IsNullOrWhiteSpace(query.Status) ? "all" : query.Status,
                string.IsNullOrWhiteSpace(query.SortBy) ? "resolutionPriority" : query.SortBy,
                string.IsNullOrWhiteSpace(query.SortOrder) ? "asc" : query.SortOrder,
                query.TenantId,
                query.SystemSuiteId,
                query.ProviderType), ct);
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
