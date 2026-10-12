namespace Evolith.Ums.Presentation.Endpoints.Configuration.Parameter;

using Evolith.Ums.Application.Configuration.Parameter.Commands;
using Evolith.Ums.Presentation.Extensions;

public static class ParameterEndpoints
{
    public static IEndpointRouteBuilder MapParameterEndpoints(this IEndpointRouteBuilder app)
    {
        // ── ParameterDefinition mutations ────────────────────────────────────
        var defs = app.MapGroup("/parameter-definitions").WithTags("Parameter Definitions");

        defs.MapPost("/", async (
            CreateParameterDefinitionCommand cmd, IMediator mediator,
            ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can manage parameter definitions." }, statusCode: 403);

            var result = await mediator.Send(cmd, ct);
            return result.ToCreated(id => $"/parameter-definitions/{id}", context);
        })
        .WithName("CreateParameterDefinition")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        defs.MapPut("/{id:guid}", async (
            Guid id, UpdateParameterDefinitionCommand cmd,
            IMediator mediator, ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can modify parameter definitions." }, statusCode: 403);

            var result = await mediator.Send(cmd with { Id = id }, ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateParameterDefinition")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        defs.MapPost("/{id:guid}/archive", async (
            Guid id, IMediator mediator, ITenantContext tenantContext,
            HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can archive parameter definitions." }, statusCode: 403);

            var result = await mediator.Send(new ArchiveParameterDefinitionCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("ArchiveParameterDefinition")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Borrado LÓGICO (el contrato HTTP no cambia: 204 en éxito). 409 si la definición todavía
        // tiene valores globales/de inquilino VIVOS; el actor debe eliminarlos antes.
        defs.MapDelete("/{id:guid}", async (
            Guid id, IMediator mediator, ITenantContext tenantContext,
            HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can delete parameter definitions." }, statusCode: 403);

            var result = await mediator.Send(new DeleteParameterDefinitionCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeleteParameterDefinition")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // ── ParameterGlobalValue mutations ───────────────────────────────────
        var gv = app.MapGroup("/parameter-definitions/{definitionId:guid}/global-values")
            .WithTags("Parameter Global Values");

        gv.MapPost("/", async (
            Guid definitionId, CreateParameterGlobalValueCommand cmd,
            IMediator mediator, ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can manage global parameter values." }, statusCode: 403);

            var result = await mediator.Send(cmd with { DefinitionId = definitionId }, ct);
            return result.ToCreated(id => $"/parameter-definitions/{definitionId}/global-values/{id}", context);
        })
        .WithName("CreateParameterGlobalValue")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        gv.MapPut("/{id:guid}", async (
            Guid definitionId, Guid id, UpdateParameterGlobalValueCommand cmd,
            IMediator mediator, ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can modify global parameter values." }, statusCode: 403);

            var result = await mediator.Send(cmd with { Id = id }, ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateParameterGlobalValue")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        gv.MapPost("/{id:guid}/publish", async (
            Guid definitionId, Guid id, IMediator mediator,
            ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can publish parameter values." }, statusCode: 403);

            var result = await mediator.Send(new PublishParameterGlobalValueCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("PublishParameterGlobalValue")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        gv.MapPost("/{id:guid}/archive", async (
            Guid definitionId, Guid id, IMediator mediator,
            ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can archive parameter values." }, statusCode: 403);

            var result = await mediator.Send(new ArchiveParameterGlobalValueCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("ArchiveParameterGlobalValue")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Borrado LÓGICO del valor global: libera la referencia que bloquea el borrado de su
        // definición (archivarlo no la libera — una fila archivada sigue siendo una referencia real).
        gv.MapDelete("/{id:guid}", async (
            Guid definitionId, Guid id, IMediator mediator,
            ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can delete parameter values." }, statusCode: 403);

            var result = await mediator.Send(new DeleteParameterGlobalValueCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeleteParameterGlobalValue")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // ── ParameterTenantValue mutations ───────────────────────────────────
        var tv = app.MapGroup("/parameter-definitions/{definitionId:guid}/tenant-values")
            .WithTags("Parameter Tenant Values");

        tv.MapPost("/", async (
            Guid definitionId, CreateParameterTenantValueCommand cmd,
            IMediator mediator, ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            // Tenant admins can only create values for their own tenant
            if (!tenantContext.IsInternalAdmin && cmd.TenantId != tenantContext.OrganizationId)
                return Results.Json(new { error = "You can only create parameter values for your own tenant." }, statusCode: 403);

            var result = await mediator.Send(cmd with { DefinitionId = definitionId }, ct);
            return result.ToCreated(id => $"/parameter-definitions/{definitionId}/tenant-values/{id}", context);
        })
        .WithName("CreateParameterTenantValue")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        tv.MapPut("/{id:guid}", async (
            Guid definitionId, Guid id, UpdateParameterTenantValueCommand cmd,
            IMediator mediator, ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(cmd with { Id = id }, ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateParameterTenantValue")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Borrado LÓGICO del override de inquilino. Guarda deliberadamente estricta (solo
        // administrador interno, como el resto de la gestión del catálogo de parámetros): el
        // endpoint no conoce el inquilino del valor sin releerlo, y ante la duda no se abre
        // una vía por la que un inquilino pudiera retirar el override de otro.
        tv.MapDelete("/{id:guid}", async (
            Guid definitionId, Guid id, IMediator mediator,
            ITenantContext tenantContext, HttpContext context, CancellationToken ct) =>
        {
            if (!tenantContext.IsInternalAdmin)
                return Results.Json(new { error = "Only internal administrators can delete parameter values." }, statusCode: 403);

            var result = await mediator.Send(new DeleteParameterTenantValueCommand(id), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeleteParameterTenantValue")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
