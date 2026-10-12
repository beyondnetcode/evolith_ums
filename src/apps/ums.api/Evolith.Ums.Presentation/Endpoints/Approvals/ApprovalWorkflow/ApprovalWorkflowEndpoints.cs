namespace Evolith.Ums.Presentation.Endpoints.Approvals.ApprovalWorkflow;

using Evolith.Ums.Application.Approvals.ApprovalWorkflow.Commands;
using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;
using Evolith.Ums.Application.Approvals.ApprovalWorkflow.Queries;
using Evolith.Ums.Application.Common;

public static class ApprovalWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapApprovalWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/approval-workflows").WithTags("ApprovalWorkflows");

        group.MapGet("/", async (
            [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? search,
            [FromQuery] string? criteria, [FromQuery] string? sortBy, [FromQuery] string? sortOrder,
            [FromQuery] Guid? tenantId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllApprovalWorkflowsQuery(
                page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, search,
                string.IsNullOrWhiteSpace(criteria) ? "name" : criteria,
                string.IsNullOrWhiteSpace(sortBy) ? "name" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder, tenantId), ct);
            return result.ToOk(context);
        }).WithName("GetAllApprovalWorkflows").Produces<PagedResult<ApprovalWorkflowDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateApprovalWorkflowCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/approval-workflows/{r.ApprovalWorkflowId}", context);
        }).WithName("CreateApprovalWorkflow").Produces<CreateApprovalWorkflowResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{workflowId:guid}/required-documents", async (Guid workflowId, AddRequiredDocumentCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { WorkflowId = workflowId }, ct);
            return result.ToNoContent(context);
        }).WithName("AddRequiredDocument")
          .WithSummary("Add a required document type to the approval workflow")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{workflowId:guid}/required-documents/{documentId:guid}", async (Guid workflowId, Guid documentId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RemoveRequiredDocumentCommand(workflowId, documentId), ct);
            return result.ToNoContent(context);
        }).WithName("RemoveRequiredDocument")
          .WithSummary("Remove a required document from the approval workflow")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
