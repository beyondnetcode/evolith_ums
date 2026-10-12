namespace Evolith.Ums.Presentation.Endpoints.Authorization.Template;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Authorization.Template.Commands;
using Evolith.Ums.Application.Authorization.Template.DTOs;
using Evolith.Ums.Application.Authorization.Template.Queries;

public static class PermissionTemplateEndpoints
{
    public static IEndpointRouteBuilder MapPermissionTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/permission-templates")
            .WithTags("PermissionTemplates");

        group.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] string? criteria,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            [FromQuery] Guid? tenantId,
            [FromQuery] Guid? systemSuiteId,
            [FromQuery] Guid? roleId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllPermissionTemplatesQuery(
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : pageSize,
                search,
                string.IsNullOrWhiteSpace(criteria) ? "version" : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "version" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder,
                tenantId,
                systemSuiteId,
                roleId), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllPermissionTemplates")
        .WithSummary("Get permission templates using server-side pagination")
        .Produces<PagedResult<PermissionTemplateDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreatePermissionTemplateCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/permission-templates/{r.TemplateId}", context);
        })
        .WithName("CreatePermissionTemplate")
        .WithSummary("Create a new permission template")
        .Produces<CreatePermissionTemplateResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{templateId:guid}/publish", async (Guid templateId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new PublishPermissionTemplateCommand(templateId), ct);
            return result.ToNoContent(context);
        })
        .WithName("PublishPermissionTemplate")
        .WithSummary("Publish a draft permission template")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{templateId:guid}/deprecate", async (Guid templateId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeprecatePermissionTemplateCommand(templateId), ct);
            return result.ToNoContent(context);
        }).WithName("DeprecatePermissionTemplate")
          .WithSummary("Deprecate a published permission template — prevents new profile assignments")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{templateId:guid}", async (Guid templateId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeletePermissionTemplateCommand(templateId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeletePermissionTemplate")
        .WithSummary("Delete a draft permission template — only Draft status allowed")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{templateId:guid}/items", async (Guid templateId, AddTemplateItemCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { TemplateId = templateId }, ct);
            // 201 con el id del ítem creado (G-053): DELETE/PUT del ítem se hacen por itemId.
            return result.ToCreated(r => $"/permission-templates/{templateId}/items/{r.ItemId}", context);
        }).WithName("AddTemplateItem")
          .WithSummary("Add a permission item (target + action + effect) to a draft template")
          .Produces<AddTemplateItemResponse>(StatusCodes.Status201Created)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE de un ítem = RETIRADA LÓGICA (ADR-0164). La ruta y su contrato —204/404/409— se
        // conservan porque el cliente ya la invoca, pero por debajo ejecuta el mismo verbo que
        // `/deactivate`: la concesión se marca, la fila no se toca. El borrado físico no se deja
        // «por si acaso» en otra ruta; simplemente ya no existe en ninguna capa.
        group.MapDelete("/{templateId:guid}/items/{itemId:guid}", async (Guid templateId, Guid itemId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateTemplateItemCommand(templateId, itemId), ct);
            return result.ToNoContent(context);
        }).WithName("RetireTemplateItem")
          .WithSummary("Retire a permission item from a draft template — logical retirement, the row survives")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{templateId:guid}/items/{itemId:guid}/effect", async (Guid templateId, Guid itemId, SetTemplateItemEffectCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { TemplateId = templateId, ItemId = itemId }, ct);
            return result.ToNoContent(context);
        }).WithName("SetTemplateItemEffect")
          .WithSummary("Override the Allow/Deny/Neutral effect of a permission item")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{templateId:guid}/items/{itemId:guid}/activate", async (Guid templateId, Guid itemId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ActivateTemplateItemCommand(templateId, itemId), ct);
            return result.ToNoContent(context);
        }).WithName("ActivateTemplateItem")
          .WithSummary("Activate a deactivated permission item")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{templateId:guid}/items/{itemId:guid}/deactivate", async (Guid templateId, Guid itemId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateTemplateItemCommand(templateId, itemId), ct);
            return result.ToNoContent(context);
        }).WithName("DeactivateTemplateItem")
          .WithSummary("Deactivate an active permission item without removing it")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
