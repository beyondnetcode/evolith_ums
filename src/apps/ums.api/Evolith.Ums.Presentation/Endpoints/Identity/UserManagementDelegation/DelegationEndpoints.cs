namespace Evolith.Ums.Presentation.Endpoints.Identity.UserManagementDelegation;

using Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;
using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;

public static class DelegationEndpoints
{
    public static IEndpointRouteBuilder MapDelegationEndpoints(this IEndpointRouteBuilder app)
    {
        // SEGURIDAD (ADR-UMS-086 endurecido, G-148): una delegación confiere autoridad de
        // administrador; todo el grupo de escritura exige autenticación (RequireAuthorization),
        // igual que los endpoints de consulta de delegación y el grupo IGA de promoción de rol.
        // La autorización fina (management-owner) y el aislamiento por inquilino se hacen cumplir
        // en la capa de aplicación (ITenantScopePolicy), y la separación de funciones en el dominio.
        var group = app.MapGroup("/delegations")
            .WithTags("Delegations")
            .RequireAuthorization();

        group.MapPost("/", async (CreateDelegationCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/delegations/{r.DelegationId}", context);
        })
        .WithName("CreateDelegation")
        .WithSummary("Create a new user management delegation")
        .Produces<CreateDelegationResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{delegationId:guid}/activate", async (Guid delegationId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateDelegationCommand(delegationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ActivateDelegation")
        .WithSummary("Activate a draft delegation")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{delegationId:guid}/revoke", async (Guid delegationId, string reason, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RevokeDelegationCommand(delegationId, reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("RevokeDelegation")
        .WithSummary("Revoke an active delegation")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{delegationId:guid}/submit-for-approval", async (Guid delegationId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SubmitDelegationForApprovalCommand(delegationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("SubmitDelegationForApproval")
        .WithSummary("Submit a draft delegation for approval (Draft → PendingApproval)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{delegationId:guid}/approve", async (Guid delegationId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ApproveDelegationCommand(delegationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ApproveDelegation")
        .WithSummary("Approve a delegation pending approval (PendingApproval → Active)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{delegationId:guid}/reject", async (Guid delegationId, string reason, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RejectDelegationCommand(delegationId, reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("RejectDelegation")
        .WithSummary("Reject a delegation pending approval (PendingApproval → Rejected)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{delegationId:guid}/expire", async (Guid delegationId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ExpireDelegationCommand(delegationId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ExpireDelegation")
        .WithSummary("Mark an active delegation as expired (background worker use)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
