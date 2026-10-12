namespace Evolith.Ums.Presentation.Endpoints.Authorization.SystemSuite;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Authorization.SystemSuite.Commands;
using Evolith.Ums.Application.Authorization.SystemSuite.DTOs;
using Evolith.Ums.Application.Authorization.SystemSuite.Queries;
using Evolith.Ums.Presentation.Endpoints.Authorization.SystemSuite.DTOs;
public static class SystemSuiteEndpoints
{
    public static IEndpointRouteBuilder MapSystemSuiteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system-suites")
            .WithTags("SystemSuites");

        group.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] string? criteria,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            [FromQuery] Guid? tenantId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllSystemSuitesQuery(
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : pageSize,
                search,
                string.IsNullOrWhiteSpace(criteria) ? "name" : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "name" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder,
                tenantId), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllSystemSuites")
        .WithSummary("Get system suites using server-side pagination")
        .Produces<PagedResult<SystemSuiteDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateSystemSuiteCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/system-suites/{r.SystemSuiteId}", context);
        })
        .WithName("CreateSystemSuite")
        .WithSummary("Create a new system suite")
        .Produces<CreateSystemSuiteResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{systemSuiteId:guid}", async (Guid systemSuiteId, UpdateSystemSuiteCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId }, ct);
            return result.ToNoContent(context);
        })
        .WithName("UpdateSystemSuite")
        .WithSummary("Update a system suite")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Updated to use PUT and accept JSON payload containing status
        group.MapPut("/{systemSuiteId:guid}/status", async (Guid systemSuiteId, SetSystemSuiteStatusPayload payload, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new SetSystemSuiteStatusCommand(systemSuiteId, payload.Status), ct);
            return result.ToNoContent(context);
        })
        .WithName("SetSystemSuiteStatus")
        .WithSummary("Set system suite status")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // Eliminación LÓGICA del sistema (G-246). El archivado sigue siendo el PUT /{id}/status →
        // Deprecated; esto elimina lo ya archivado y sin referencias vivas. La fila NO se borra: se
        // marca `Deleted` y desaparece de las lecturas. 409 si el sistema sigue vigente o si algo
        // vivo lo apunta —la respuesta enumera qué—.
        group.MapDelete("/{systemSuiteId:guid}", async (Guid systemSuiteId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteSystemSuiteCommand(systemSuiteId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeleteSystemSuite")
        .WithSummary("Logically delete a deprecated system suite that has no live dependents")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // ── Module lifecycle ─────────────────────────────────────────────────

        group.MapPost("/{systemSuiteId:guid}/modules", async (Guid systemSuiteId, AddModuleCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId }, ct);
            // 201 con el id del módulo creado (G-053): el cliente ya no necesita un GET para obtenerlo.
            return result.ToCreated(r => $"/system-suites/{systemSuiteId}/modules/{r.ModuleId}", context);
        }).WithName("AddModule")
          .WithSummary("Add a module to the system suite")
          .Produces<AddModuleResponse>(StatusCodes.Status201Created)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{systemSuiteId:guid}/modules/{moduleId:guid}", async (Guid systemSuiteId, Guid moduleId, UpdateModuleCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, ModuleId = moduleId }, ct);
            return result.ToNoContent(context);
        }).WithName("UpdateModule")
          .WithSummary("Update module name, description, or sort order")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{systemSuiteId:guid}/modules/{moduleId:guid}", async (Guid systemSuiteId, Guid moduleId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RemoveModuleCommand(systemSuiteId, moduleId), ct);
            return result.ToNoContent(context);
        }).WithName("RemoveModule")
          .WithSummary("Remove an inactive module from the system suite")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{systemSuiteId:guid}/modules/{moduleId:guid}/activate", async (Guid systemSuiteId, Guid moduleId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateModuleCommand(systemSuiteId, moduleId), ct);
            return result.ToNoContent(context);
        }).WithName("ActivateModule")
          .WithSummary("Activate a deactivated module")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{systemSuiteId:guid}/modules/{moduleId:guid}/deactivate", async (Guid systemSuiteId, Guid moduleId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateModuleCommand(systemSuiteId, moduleId), ct);
            return result.ToNoContent(context);
        }).WithName("DeactivateModule")
          .WithSummary("Deactivate an active module")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        // ── Node lifecycle (árbol recursivo, ADR-0090) ───────────────────────

        group.MapPost("/{systemSuiteId:guid}/modules/{moduleId:guid}/nodes", async (Guid systemSuiteId, Guid moduleId, AddNodeCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, ModuleId = moduleId }, ct);
            // 201 con el id del nodo creado (G-053): DELETE/GET de nodo se hacen por id, ya no se fuerza un GET.
            return result.ToCreated(r => $"/system-suites/{systemSuiteId}/modules/{moduleId}/nodes/{r.NodeId}", context);
        }).WithName("AddNode")
          .WithSummary("Add a node (root when ParentNodeId is null, else child) to a module tree")
          .Produces<AddNodeResponse>(StatusCodes.Status201Created)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{systemSuiteId:guid}/modules/{moduleId:guid}/nodes/{nodeId:guid}", async (Guid systemSuiteId, Guid moduleId, Guid nodeId, UpdateNodeCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, ModuleId = moduleId, NodeId = nodeId }, ct);
            return result.ToNoContent(context);
        }).WithName("UpdateNode").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{systemSuiteId:guid}/modules/{moduleId:guid}/nodes/{nodeId:guid}", async (Guid systemSuiteId, Guid moduleId, Guid nodeId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RemoveNodeCommand(systemSuiteId, moduleId, nodeId), ct);
            return result.ToNoContent(context);
        }).WithName("RemoveNode").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{systemSuiteId:guid}/modules/{moduleId:guid}/nodes/{nodeId:guid}/status", async (Guid systemSuiteId, Guid moduleId, Guid nodeId, SetNodeStatusCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, ModuleId = moduleId, NodeId = nodeId }, ct);
            return result.ToNoContent(context);
        }).WithName("SetNodeStatus").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{systemSuiteId:guid}/modules/{moduleId:guid}/nodes/{nodeId:guid}/actions", async (Guid systemSuiteId, Guid moduleId, Guid nodeId, LinkNodeActionCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, ModuleId = moduleId, NodeId = nodeId }, ct);
            return result.ToNoContent(context);
        }).WithName("LinkNodeAction").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{systemSuiteId:guid}/modules/{moduleId:guid}/nodes/{nodeId:guid}/actions/{actionCode}", async (Guid systemSuiteId, Guid moduleId, Guid nodeId, string actionCode, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new UnlinkNodeActionCommand(systemSuiteId, moduleId, nodeId, actionCode), ct);
            return result.ToNoContent(context);
        }).WithName("UnlinkNodeAction").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{systemSuiteId:guid}/modules/{moduleId:guid}/nodes/{nodeId:guid}/metadata", async (Guid systemSuiteId, Guid moduleId, Guid nodeId, SetNodeMetadataCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, ModuleId = moduleId, NodeId = nodeId }, ct);
            return result.ToNoContent(context);
        }).WithName("SetNodeMetadata").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        // ── App settings ─────────────────────────────────────────────────────

        group.MapPost("/{systemSuiteId:guid}/app-settings", async (Guid systemSuiteId, AddAppSettingCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId }, ct);
            if (result.IsSuccess)
                return Results.Created($"/system-suites/{systemSuiteId}/app-settings", result);
            var (status, title) = DomainErrorStatusMapper.Map(result.Error);
            var problem = new ProblemDetails
            {
                Title = title,
                Status = status,
                Detail = result.Error,
                Instance = context?.Request?.Path,
                Extensions = { ["timestamp"] = DateTimeOffset.UtcNow }
            };
            return Results.Problem(problem);
        }).WithName("AddAppSetting")
          .WithSummary("Add a configuration key-value pair to the system suite")
          .Produces(StatusCodes.Status201Created)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{systemSuiteId:guid}/app-settings/{key}", async (Guid systemSuiteId, string key, UpdateAppSettingCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, Key = key }, ct);
            return result.ToNoContent(context);
        }).WithName("UpdateAppSetting")
          .WithSummary("Update the value of an existing app setting")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{systemSuiteId:guid}/app-settings/{key}", async (Guid systemSuiteId, string key, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RemoveAppSettingCommand(systemSuiteId, key), ct);
            return result.ToNoContent(context);
        }).WithName("RemoveAppSetting")
          .WithSummary("Remove an app setting from the system suite")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound);

        // ── Actions ──────────────────────────────────────────────────────────

        group.MapPost("/{systemSuiteId:guid}/actions", async (Guid systemSuiteId, RegisterActionCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId }, ct);
            // 201 con el id de la acción creada (G-053). La ruta usa el code, clave de negocio del recurso.
            return result.ToCreated(r => $"/system-suites/{systemSuiteId}/actions/{r.Code}", context);
        }).WithName("RegisterAction")
          .WithSummary("Register a new action code that can be used in permission templates")
          .Produces<RegisterActionResponse>(StatusCodes.Status201Created)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{systemSuiteId:guid}/actions/{code}", async (Guid systemSuiteId, string code, RenameActionCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, Code = code }, ct);
            return result.ToNoContent(context);
        }).WithName("RenameAction")
          .WithSummary("Rename an action in the system suite")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{systemSuiteId:guid}/actions/{code}", async (Guid systemSuiteId, string code, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RemoveActionCommand(systemSuiteId, code), ct);
            return result.ToNoContent(context);
        }).WithName("RemoveAction")
          .WithSummary("Remove an unused action from the system suite")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        // ── Domain Resources ─────────────────────────────────────────────────

        group.MapPost("/{systemSuiteId:guid}/domain-resources", async (Guid systemSuiteId, AddDomainResourceCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId }, ct);
            // 201 con el id del recurso de dominio creado (G-053): DELETE/PUT se hacen por id.
            return result.ToCreated(r => $"/system-suites/{systemSuiteId}/domain-resources/{r.DomainResourceId}", context);
        }).WithName("AddDomainResource")
          .WithSummary("Add a domain resource (Aggregate or Entity) to the system suite")
          .Produces<AddDomainResourceResponse>(StatusCodes.Status201Created)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{systemSuiteId:guid}/domain-resources/{domainResourceId:guid}", async (Guid systemSuiteId, Guid domainResourceId, UpdateDomainResourceCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { SystemSuiteId = systemSuiteId, DomainResourceId = domainResourceId }, ct);
            return result.ToNoContent(context);
        }).WithName("UpdateDomainResource")
          .WithSummary("Update a domain resource name or description")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{systemSuiteId:guid}/domain-resources/{domainResourceId:guid}", async (Guid systemSuiteId, Guid domainResourceId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RemoveDomainResourceCommand(systemSuiteId, domainResourceId), ct);
            return result.ToNoContent(context);
        }).WithName("RemoveDomainResource")
          .WithSummary("Remove a domain resource from the system suite")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
