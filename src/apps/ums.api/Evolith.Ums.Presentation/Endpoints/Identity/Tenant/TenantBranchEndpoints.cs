namespace Evolith.Ums.Presentation.Endpoints.Identity.Tenant;

using Evolith.Ums.Application.Identity.Tenant.Branch.Commands;
using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;

public static class TenantBranchEndpoints
{
    public static IEndpointRouteBuilder MapTenantBranchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tenants/{tenantId:guid}/branches")
            .WithTags("Tenant Branches");

        group.MapPost("/", async (
            Guid tenantId,
            [FromBody] AddBranchRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var command = new AddBranchCommand(tenantId, request.Code, request.Name, request.GeofencingMetadata);
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/tenants/{r.TenantId}/branches", context);
        })
        .WithName("AddBranch")
        .WithSummary("Add a branch to an existing tenant")
        .Produces<AddBranchResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{branchId:guid}", async (
            Guid tenantId,
            Guid branchId,
            [FromBody] UpdateBranchRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var command = new UpdateBranchCommand(tenantId, branchId, request.Name, request.GeofencingMetadata);
            var result = await mediator.Send(command, ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateBranch")
        .WithSummary("Update a branch's editable data (name, geofencing)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ADR-0164: el verbo HTTP sigue siendo DELETE —es el verbo REST de la baja y el frontend ya
        // lo invoca—, pero lo que ocurre detrás es un cierre DEFINITIVO con borrado lógico: la fila
        // permanece y el código queda ocupado para siempre. No hay borrado físico que ofrecer.
        group.MapDelete("/{branchId:guid}", async (
            Guid tenantId,
            Guid branchId,
            [FromQuery] string? reason,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new CloseBranchCommand(tenantId, branchId, reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("CloseBranch")
        .WithSummary("Cierra definitivamente una sucursal (borrado lógico; la fila permanece y el código no se libera)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{branchId:guid}/deactivate", async (
            Guid tenantId,
            Guid branchId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateBranchCommand(tenantId, branchId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeactivateBranch")
        .WithSummary("Deactivate a branch")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{branchId:guid}/reactivate", async (
            Guid tenantId,
            Guid branchId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ReactivateBranchCommand(tenantId, branchId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ReactivateBranch")
        .WithSummary("Reactivate a branch")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

public sealed record AddBranchRequest(string Code, string Name, string? GeofencingMetadata);
public sealed record UpdateBranchRequest(string Name, string? GeofencingMetadata);
