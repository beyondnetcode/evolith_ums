namespace Evolith.Ums.Presentation.Endpoints.Approvals.UserDocument;

using Evolith.Ums.Application.Approvals.UserDocument.Commands;
using Evolith.Ums.Application.Approvals.UserDocument.DTOs;
using Evolith.Ums.Application.Approvals.UserDocument.Queries;
using Evolith.Ums.Application.Common;

public static class UserDocumentEndpoints
{
    public static IEndpointRouteBuilder MapUserDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/user-documents").WithTags("UserDocuments");

        group.MapGet("/", async (
            [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? search,
            [FromQuery] string? criteria, [FromQuery] string? status, [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder, [FromQuery] Guid? userId, [FromQuery] Guid? tenantId,
            IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllUserDocumentsQuery(
                page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, search,
                string.IsNullOrWhiteSpace(criteria) ? "status" : criteria,
                string.IsNullOrWhiteSpace(status) ? "all" : status,
                string.IsNullOrWhiteSpace(sortBy) ? "status" : sortBy,
                string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder, userId, tenantId), ct);
            return result.ToOk(context);
        }).WithName("GetAllUserDocuments").Produces<PagedResult<UserDocumentDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (UploadUserDocumentCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/user-documents/{r.UserDocumentId}", context);
        }).WithName("UploadUserDocument").Produces<UploadUserDocumentResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/{id:guid}/validate", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ValidateUserDocumentCommand(id), ct);
            return result.ToNoContent(context);
        }).WithName("ValidateUserDocument").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{userDocumentId:guid}/reject", async (Guid userDocumentId, RejectUserDocumentCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { UserDocumentId = userDocumentId }, ct);
            return result.ToNoContent(context);
        }).WithName("RejectUserDocument")
          .WithSummary("Reject a user document — requires re-upload before the workflow can proceed")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userDocumentId:guid}/expire", async (Guid userDocumentId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new ExpireUserDocumentCommand(userDocumentId), ct);
            return result.ToNoContent(context);
        }).WithName("ExpireUserDocument")
          .WithSummary("Mark a user document as expired (background-worker use)")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userDocumentId:guid}/re-upload", async (Guid userDocumentId, ReUploadUserDocumentCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { UserDocumentId = userDocumentId }, ct);
            return result.ToNoContent(context);
        }).WithName("ReUploadUserDocument")
          .WithSummary("Re-upload a rejected or expired document with a new file and dates")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound)
          .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{userDocumentId:guid}/enforcement", async (
            Guid userDocumentId,
            RecordEnforcementExecutedRequest body,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new RecordEnforcementExecutedCommand(userDocumentId, body.Action), ct);
            return result.ToNoContent(context);
        }).WithName("RecordEnforcementExecuted")
          .WithSummary("Record a compliance enforcement action executed against a user document (system use)")
          .RequireAuthorization()
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

public sealed record RecordEnforcementExecutedRequest(string Action);
