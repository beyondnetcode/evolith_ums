namespace Evolith.Ums.Presentation.Endpoints.Identity.UserManagementDelegation.Queries;

#pragma warning disable S125

using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;
using Evolith.Ums.Application.Identity.UserManagementDelegation.Queries;

public static class DelegationQueryEndpoints
{
    public static IEndpointRouteBuilder MapDelegationQueryEndpoints(this IEndpointRouteBuilder app)
    {
        // G-041 (SEGURIDAD): las delegaciones exponen relaciones de gestión (PII);
        // solo usuarios autenticados.
        var group = app.MapGroup("/delegations")
            .WithTags("Delegations - Queries")
            .RequireAuthorization();

        group.MapGet("/", async (IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllDelegationsQuery(), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllDelegations")
        .WithSummary("Get all delegations (admin-scoped by tenant via RLS)")
        .Produces<IReadOnlyList<DelegationDto>>(StatusCodes.Status200OK);

        group.MapGet("/{delegationId:guid}", async (Guid delegationId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDelegationByIdQuery(delegationId), ct);
            return result.ToOk(context);
        })
        .WithName("GetDelegationById")
        .WithSummary("Get delegation by ID")
        .Produces<DelegationDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/by-delegated-admin/{delegatedAdminId:guid}", async (Guid delegatedAdminId, Guid tenantId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDelegationsByDelegatedAdminQuery(delegatedAdminId, tenantId), ct);
            return result.ToOk(context);
        })
        .WithName("GetDelegationsByDelegatedAdmin")
        .WithSummary("Get all delegations received by an admin")
        .Produces<IReadOnlyList<DelegationDto>>(StatusCodes.Status200OK);

        group.MapGet("/by-delegating-admin/{delegatingAdminId:guid}", async (Guid delegatingAdminId, Guid tenantId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDelegationsByDelegatingAdminQuery(delegatingAdminId, tenantId), ct);
            return result.ToOk(context);
        })
        .WithName("GetDelegationsByDelegatingAdmin")
        .WithSummary("Get all delegations granted by an admin")
        .Produces<IReadOnlyList<DelegationDto>>(StatusCodes.Status200OK);

        return app;
    }
}
