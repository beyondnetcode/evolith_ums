namespace Evolith.Ums.Presentation.Endpoints.Approvals.ApprovalRequest.Queries;

using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;

public static class ApprovalRequestQueryEndpoints
{
    public static IEndpointRouteBuilder MapApprovalRequestQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/approval-requests").WithTags("ApprovalRequests - Queries");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetApprovalRequestByIdQuery(id), ct);
            return result.ToOk(context);
        }).WithName("GetApprovalRequestById").Produces<ApprovalRequestDto>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
