namespace Evolith.Ums.Presentation.Endpoints.Approvals.NotificationRule;

using Evolith.Ums.Application.Approvals.NotificationRule.Commands;
using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;
using Evolith.Ums.Application.Approvals.NotificationRule.Queries;
using Evolith.Ums.Application.Common;

public static class NotificationRuleEndpoints
{
    public static IEndpointRouteBuilder MapNotificationRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notification-rules").WithTags("NotificationRules");

        group.MapGet("/", async (
            [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? search,
            [FromQuery] string? criteria, [FromQuery] string? status, [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder, [FromQuery] Guid? tenantId,
            IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllNotificationRulesQuery(
                page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, search,
                string.IsNullOrWhiteSpace(criteria) ? "channel" : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "channel" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder, tenantId), ct);
            return result.ToOk(context);
        }).WithName("GetAllNotificationRules").Produces<PagedResult<NotificationRuleDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateNotificationRuleCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/notification-rules/{r.NotificationRuleId}", context);
        }).WithName("CreateNotificationRule").Produces<CreateNotificationRuleResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateNotificationRuleCommand(id), ct);
            return result.ToNoContent(context);
        }).WithName("DeactivateNotificationRule").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reactivate", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ReactivateNotificationRuleCommand(id), ct);
            return result.ToNoContent(context);
        }).WithName("ReactivateNotificationRule")
          .WithSummary("Reactivate a previously deactivated notification rule")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{notificationRuleId:guid}/recipient", async (Guid notificationRuleId, UpdateNotificationRuleRecipientCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { NotificationRuleId = notificationRuleId }, ct);
            return result.ToNoContent(context);
        }).WithName("UpdateNotificationRuleRecipient")
          .WithSummary("Update the recipient address of a notification rule")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{notificationRuleId:guid}/channel", async (Guid notificationRuleId, UpdateNotificationRuleChannelCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { NotificationRuleId = notificationRuleId }, ct);
            return result.ToNoContent(context);
        }).WithName("UpdateNotificationRuleChannel")
          .WithSummary("Update the delivery channel of a notification rule")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
