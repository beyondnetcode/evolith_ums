namespace Evolith.Ums.Presentation.Endpoints.Approvals.ApprovalRequest;

using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Approvals.ApprovalRequest.Commands;
using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;
using Evolith.Ums.Application.Approvals.ApprovalRequest.Queries;

public static class ApprovalRequestEndpoints
{
    public static IEndpointRouteBuilder MapApprovalRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/approval-requests").WithTags("ApprovalRequests");

        group.MapGet("/", async (
            [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? search,
            [FromQuery] string? criteria, [FromQuery] string? status, [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder, [FromQuery] Guid? tenantId, [FromQuery] Guid? userId,
            IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllApprovalRequestsQuery(
                page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, search,
                string.IsNullOrWhiteSpace(criteria) ? "status" : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "status" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder, tenantId, userId), ct);
            return result.ToOk(context);
        }).WithName("GetAllApprovalRequests").Produces<PagedResult<ApprovalRequestDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateApprovalRequestCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/approval-requests/{r.ApprovalRequestId}", context);
        }).WithName("CreateApprovalRequest").Produces<CreateApprovalRequestResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/approve", async (
            Guid id, ApproveRequestBody body, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ApproveRequestCommand(id, body.GrantedRoleId, body.DecisionReason), ct);
            return result.ToNoContent(context);
        })
        .WithName("ApproveRequest")
        .WithSummary("Approve a pending profile request, assigning a final role (may differ from the requested one).")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reject", async (
            Guid id, RejectRequestBody body, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new RejectRequestCommand(id, body.DecisionReason), ct);
            return result.ToNoContent(context);
        })
        .WithName("RejectRequest")
        .WithSummary("Deny a pending profile request with an optional reason.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}

public sealed record ApproveRequestBody(Guid GrantedRoleId, string? DecisionReason = null);
public sealed record RejectRequestBody(string? DecisionReason = null);
