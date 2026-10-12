namespace Evolith.Ums.Presentation.Endpoints.Identity.Tenant.Queries;

using Evolith.Ums.Application.Identity.Tenant.Branch.DTOs;
using Evolith.Ums.Application.Identity.Tenant.Branch.Queries;

public static class BranchQueryEndpoints
{
    public static IEndpointRouteBuilder MapBranchQueryEndpoints(this IEndpointRouteBuilder app)
    {
        // G-041 (SEGURIDAD): la estructura de sucursales del tenant exige autenticación.
        var group = app.MapGroup("/tenants/{tenantId:guid}/branches")
            .WithTags("Branches - Queries")
            .RequireAuthorization();

        group.MapGet("/", async (
            Guid tenantId,
            [FromQuery] bool? includeClosed,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetBranchesByTenantIdQuery(tenantId, includeClosed ?? false), ct);
            return result.ToOk(context);
        })
        .WithName("GetBranchesByTenantId")
        .WithSummary("Get all branches for a tenant (las cerradas definitivamente se excluyen salvo includeClosed=true)")
        .Produces<IReadOnlyList<BranchDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ADR-0164: la BITÁCORA de la sucursal. Existe porque el estado solo dice cómo está hoy, y la
        // pregunta de auditoría de un operador aduanero es cómo estaba cuando salió un despacho de
        // hace años. Responde también para sucursales cerradas: ese es su caso de uso principal.
        group.MapGet("/{branchId:guid}/bitacora", async (
            Guid tenantId,
            Guid branchId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetBranchLifecycleQuery(tenantId, branchId), ct);
            return result.ToOk(context);
        })
        .WithName("GetBranchLifecycle")
        .WithSummary("Bitácora de episodios de una sucursal: apertura, bajas, reaperturas y cierre definitivo")
        .Produces<IReadOnlyList<BranchLifecycleEntryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
