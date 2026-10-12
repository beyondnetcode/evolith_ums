namespace Evolith.Ums.Presentation.Endpoints.Iga.RolePromotionRequest;

#pragma warning disable S125

using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Application.IGA.RolePromotion.Commands;

/// <summary>
/// Endpoints de escritura de la máquina de estados de promoción de rol (IGA, ADR-UMS-093, G-052).
/// Cada transición se expone como <c>POST</c> a una subruta de acción, coherente con el estilo del
/// repositorio (Approvals/Delegation). Todo el grupo exige autenticación (<c>RequireAuthorization</c>)
/// y queda acotado por inquilino en la capa de aplicación (<c>ITenantScopePolicy</c>); no se admite
/// escritura anónima ni cruzada (cerrando el antipatrón de G-040/G-041).
/// </summary>
public static class RolePromotionRequestEndpoints
{
    public static IEndpointRouteBuilder MapRolePromotionRequestEndpoints(this IEndpointRouteBuilder app)
    {
        // SEGURIDAD (ADR-UMS-093, G-052): la promoción de rol es gobernanza sensible;
        // solo usuarios autenticados y acotados por inquilino.
        var group = app.MapGroup("/role-promotion-requests")
            .WithTags("IGA - RolePromotionRequests")
            .RequireAuthorization();

        group.MapPost("/", async (CreateRolePromotionRequestCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/role-promotion-requests/{r.RolePromotionRequestId}", context);
        })
        .WithName("CreateRolePromotionRequest")
        .WithSummary("Crea una solicitud de promoción de rol en estado Draft (FR-060).")
        .Produces<CreateRolePromotionRequestResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/submit", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SubmitRolePromotionCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("SubmitRolePromotionRequest")
        .WithSummary("Draft → PendingEligibilityCheck: congela el RiskScore (FR-061).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/confirm-eligibility", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ConfirmRolePromotionEligibilityCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("ConfirmRolePromotionEligibility")
        .WithSummary("PendingEligibilityCheck → PendingManagerApproval o Rejected, fail-closed (FR-062).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/manager-approve", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ManagerApproveRolePromotionCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("ManagerApproveRolePromotion")
        .WithSummary("PendingManagerApproval → PendingSecurityReview o Approved según el RiskScore (FR-060).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/manager-reject", async (Guid id, RolePromotionDecisionReasonBody body, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ManagerRejectRolePromotionCommand(id, body.Reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("ManagerRejectRolePromotion")
        .WithSummary("PendingManagerApproval → Rejected con motivo (FR-060).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/security-approve", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SecurityApproveRolePromotionCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("SecurityApproveRolePromotion")
        .WithSummary("PendingSecurityReview → Approved (FR-060).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/security-reject", async (Guid id, RolePromotionDecisionReasonBody body, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SecurityRejectRolePromotionCommand(id, body.Reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("SecurityRejectRolePromotion")
        .WithSummary("PendingSecurityReview → Rejected con motivo (FR-060).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/execute", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ExecuteRolePromotionCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("ExecuteRolePromotion")
        .WithSummary("Approved → Executed: aplica el cambio de rol (INV-RPR5).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/verify", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new VerifyRolePromotionCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("VerifyRolePromotion")
        .WithSummary("Executed → Verified: verificación post-ejecución por un auditor (INV-RPR5).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", async (Guid id, RolePromotionDecisionReasonBody body, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new CancelRolePromotionCommand(id, body.Reason), ct);
            return result.ToNoContent(context);
        })
        .WithName("CancelRolePromotion")
        .WithSummary("Draft → Cancelled: el solicitante cancela antes de enviar (FR-060).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

/// <summary>Cuerpo de las transiciones de rechazo/cancelación: el motivo de la decisión.</summary>
public sealed record RolePromotionDecisionReasonBody(string Reason);
