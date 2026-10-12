namespace Evolith.Ums.Presentation.Endpoints.Approvals.UserDocument;

using Evolith.Ums.Application.Approvals.UserDocument.Commands;
using Evolith.Ums.Application.Approvals.UserDocument.DTOs;
using Evolith.Ums.Application.Approvals.UserDocument.Queries;
using Evolith.Ums.Application.Common;
using Evolith.Ums.Presentation.Endpoints.Common;

public static class UserDocumentEndpoints
{
    public static IEndpointRouteBuilder MapUserDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/user-documents").WithTags("UserDocuments");

        group.MapGet("/", async (
            [AsParameters] TenantUserPagedQueryParameters query,
            IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllUserDocumentsQuery(
                query.Page <= 0 ? 1 : query.Page, query.PageSize <= 0 ? 20 : query.PageSize, query.Search,
                string.IsNullOrWhiteSpace(query.Criteria) ? "status" : query.Criteria,
                string.IsNullOrWhiteSpace(query.Status) ? "all" : query.Status,
                string.IsNullOrWhiteSpace(query.SortBy) ? "status" : query.SortBy,
                string.IsNullOrWhiteSpace(query.SortOrder) ? "asc" : query.SortOrder, query.UserId, query.TenantId), ct);
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
