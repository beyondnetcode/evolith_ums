namespace Evolith.Ums.Presentation.Endpoints.Iga.RoleMaturityStatus.Queries;

using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Application.IGA.RoleMaturity.Queries;

/// <summary>
/// Endpoints de lectura del estado de madurez de rol (IGA, ADR-UMS-093, FR-062, G-052).
/// El grupo exige autenticación y la acotación por inquilino se aplica en la capa de aplicación
/// (<c>ITenantScopePolicy</c>); no se admite lectura anónima ni cruzada (G-041).
/// </summary>
public static class RoleMaturityStatusQueryEndpoints
{
    public static IEndpointRouteBuilder MapRoleMaturityStatusQueryEndpoints(this IEndpointRouteBuilder app)
    {
        // SEGURIDAD (ADR-UMS-093, G-052/G-041): madurez de rol es gobernanza; solo autenticados y por inquilino.
        var group = app.MapGroup("/role-maturity-status")
            .WithTags("IGA - RoleMaturityStatus - Queries")
            .RequireAuthorization();

        group.MapGet("/users/{userId:guid}", async (
            Guid userId,
            [FromQuery] Guid tenantId,
            [FromQuery] Guid? roleId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRoleMaturityStatusByUserQuery(tenantId, userId, roleId), ct);
            return result.ToOk(context);
        })
        .WithName("GetRoleMaturityStatusByUser")
        .WithSummary("Obtiene el/los estado(s) de madurez de un usuario, acotado por inquilino; si se indica roleId, el de ese rol (FR-062).")
        .Produces<IReadOnlyList<RoleMaturityStatusDto>>(StatusCodes.Status200OK);

        return app;
    }
}
