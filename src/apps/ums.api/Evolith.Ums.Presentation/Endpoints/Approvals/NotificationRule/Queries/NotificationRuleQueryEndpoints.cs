namespace Evolith.Ums.Presentation.Endpoints.Approvals.NotificationRule.Queries;

using Evolith.Ums.Application.Approvals.NotificationRule.DTOs;
using Evolith.Ums.Application.Approvals.NotificationRule.Queries;

public static class NotificationRuleQueryEndpoints
{
    public static IEndpointRouteBuilder MapNotificationRuleQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/notification-rules").WithTags("NotificationRules - Queries");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetNotificationRuleByIdQuery(id), ct);
            return result.ToOk(context);
        }).WithName("GetNotificationRuleById").Produces<NotificationRuleDto>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
