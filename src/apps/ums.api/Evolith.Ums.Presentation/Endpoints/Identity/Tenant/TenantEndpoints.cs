namespace Evolith.Ums.Presentation.Endpoints.Identity.Tenant;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Identity.Tenant.Commands;
using Evolith.Ums.Application.Identity.Tenant.DTOs;
using Evolith.Ums.Application.Identity.Tenant.Queries;
using Evolith.Ums.Application.Identity.Tenant.SignupRequests.Commands;
using Evolith.Ums.Application.Identity.Tenant.SignupRequests.DTOs;
using Evolith.Ums.Application.Identity.Tenant.SignupRequests.Queries;

public sealed record SetManagementOwnerRequest(bool Value);

// FS-26 (G-024): datos generales editables del tenant. El Code es inmutable, no se envía.
public sealed record UpdateTenantRequest(string Name, string Type, string? CompanyReference);

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tenants")
            .WithTags("Tenants");

        // ── Queries ──────────────────────────────────────────────────────────

        group.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] string? criteria,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllTenantsQuery(
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : pageSize,
                search,
                // criteria = campo de búsqueda; se pasa tal cual (null si no se envía). El handler/repo
                // cae a sortBy por compatibilidad. Antes se forzaba a "name", lo que impedía buscar por
                // código salvo enviando criteria=code explícito (G-014 residual: GetTenants_AfterCreate
                // usaba sortBy=code sin criteria y dejaba de encontrar el tenant).
                string.IsNullOrWhiteSpace(criteria) ? null : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "name" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllTenants")
        .WithSummary("Get tenants using server-side pagination")
        // G-041 (SEGURIDAD): el listado de tenants exige autenticación.
        .RequireAuthorization()
        .Produces<PagedResult<TenantDto>>(StatusCodes.Status200OK);

        // ── Commands ─────────────────────────────────────────────────────────

        group.MapPost("/", async (CreateTenantCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/tenants/{r.TenantId}", context);
        })
        .WithName("CreateTenant")
        .WithSummary("Create a new tenant")
        .Produces<CreateTenantResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{tenantId:guid}/activate", async (Guid tenantId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateTenantCommand(tenantId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ActivateTenant")
        .WithSummary("Activate a suspended tenant")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{tenantId:guid}/suspend", async (Guid tenantId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SuspendTenantCommand(tenantId), ct);
            return result.ToNoContent(context);
        })
        .WithName("SuspendTenant")
        .WithSummary("Suspend an active tenant")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{tenantId:guid}", async (
            Guid tenantId,
            UpdateTenantRequest body,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new UpdateTenantCommand(tenantId, body.Name, body.Type, body.CompanyReference), ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateTenant")
        .WithSummary("Update a tenant's general data (name, type, company reference)")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{tenantId:guid}/set-management-owner", async (
            Guid tenantId,
            SetManagementOwnerRequest body,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new SetManagementOwnerCommand(tenantId, body.Value), ct);
            return result.ToNoContent(context);
        })
        .WithName("SetManagementOwner")
        .WithSummary("Grant or revoke UMS management ownership for a tenant")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/signup-requests", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetPendingTenantSignupRequestsQuery(), ct);
            return result.ToOk(context);
        })
        .WithName("GetPendingTenantSignupRequests")
        .WithSummary("Get pending tenant signup requests for the internal admin.")
        .RequireAuthorization()
        .Produces<IReadOnlyList<TenantSignupRequestDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/signup-requests/{tenantSignupRequestId:guid}/approve", async (
            Guid tenantSignupRequestId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new ApproveTenantSignupCommand(tenantSignupRequestId), ct);
            return result.ToCreated(r => $"/tenants/{r.TenantId}", context);
        })
        .WithName("ApproveTenantSignupRequest")
        .WithSummary("Approve a pending tenant signup request.")
        .RequireAuthorization()
        .Produces<ApproveTenantSignupResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
