namespace Evolith.Ums.Presentation.Endpoints.Iga.RolePromotionRequest.Queries;

using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Application.IGA.RolePromotion.Queries;

/// <summary>
/// Endpoints de lectura de las solicitudes de promoción de rol (IGA, ADR-UMS-093, G-052).
/// El grupo exige autenticación y la acotación por inquilino se aplica en la capa de aplicación
/// (<c>ITenantScopePolicy</c>): un usuario regular queda ceñido a su inquilino, evitando la lectura
/// anónima y cruzada señalada en G-041.
/// </summary>
public static class RolePromotionRequestQueryEndpoints
{
    public static IEndpointRouteBuilder MapRolePromotionRequestQueryEndpoints(this IEndpointRouteBuilder app)
    {
        // SEGURIDAD (ADR-UMS-093, G-052/G-041): datos de gobernanza; solo autenticados y por inquilino.
        var group = app.MapGroup("/role-promotion-requests")
            .WithTags("IGA - RolePromotionRequests - Queries")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? status,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ListRolePromotionRequestsQuery(tenantId, status), ct);
            return result.ToOk(context);
        })
        .WithName("ListRolePromotionRequests")
        .WithSummary("Lista solicitudes de promoción de rol acotadas por inquilino y, opcionalmente, por estado.")
        .Produces<IReadOnlyList<RolePromotionRequestDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRolePromotionRequestByIdQuery(id), ct);
            return result.ToOk(context);
        })
        .WithName("GetRolePromotionRequestById")
        .WithSummary("Obtiene una solicitud de promoción de rol por su identificador, acotada por inquilino.")
        .Produces<RolePromotionRequestDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
