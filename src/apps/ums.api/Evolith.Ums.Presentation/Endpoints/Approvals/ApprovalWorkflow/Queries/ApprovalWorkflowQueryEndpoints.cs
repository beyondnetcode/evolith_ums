namespace Evolith.Ums.Presentation.Endpoints.Approvals.ApprovalWorkflow.Queries;

using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;
using Evolith.Ums.Application.Approvals.ApprovalWorkflow.Queries;

public static class ApprovalWorkflowQueryEndpoints
{
    public static IEndpointRouteBuilder MapApprovalWorkflowQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/approval-workflows").WithTags("ApprovalWorkflows - Queries");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetApprovalWorkflowByIdQuery(id), ct);
            return result.ToOk(context);
        }).WithName("GetApprovalWorkflowById").Produces<ApprovalWorkflowDto>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
