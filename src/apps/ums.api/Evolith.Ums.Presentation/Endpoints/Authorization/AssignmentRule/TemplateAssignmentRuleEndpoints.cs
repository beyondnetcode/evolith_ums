namespace Evolith.Ums.Presentation.Endpoints.Authorization.AssignmentRule;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Authorization.AssignmentRule.Commands;
using Evolith.Ums.Application.Authorization.AssignmentRule.DTOs;
using Evolith.Ums.Application.Authorization.AssignmentRule.Queries;

public static class TemplateAssignmentRuleEndpoints
{
    public static IEndpointRouteBuilder MapTemplateAssignmentRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/template-assignment-rules")
            .WithTags("TemplateAssignmentRules");

        group.MapGet("/", async (
            [FromQuery] Guid tenantId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAssignmentRulesByTenantQuery(tenantId), ct);
            return result.ToOk(context);
        })
        .WithName("GetAssignmentRulesByTenant")
        .WithSummary("Get all template auto-assignment rules for a tenant")
        .Produces<IReadOnlyList<AssignmentRuleDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateAssignmentRuleCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/template-assignment-rules/{r.RuleId}", context);
        })
        .WithName("CreateTemplateAssignmentRule")
        .WithSummary("Create a new template auto-assignment rule")
        .Produces<CreateAssignmentRuleResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{ruleId:guid}/deactivate", async (Guid ruleId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateAssignmentRuleCommand(ruleId), ct);
            return result.ToNoContent(context);
        })
        .WithName("DeactivateTemplateAssignmentRule")
        .WithSummary("Deactivate a template assignment rule — it will no longer trigger on profile creation")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{ruleId:guid}/reactivate", async (Guid ruleId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ReactivateAssignmentRuleCommand(ruleId), ct);
            return result.ToNoContent(context);
        })
        .WithName("ReactivateTemplateAssignmentRule")
        .WithSummary("Reactivate a previously deactivated template assignment rule")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
