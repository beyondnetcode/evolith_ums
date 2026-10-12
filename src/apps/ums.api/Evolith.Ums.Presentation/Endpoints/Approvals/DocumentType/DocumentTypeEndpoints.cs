namespace Evolith.Ums.Presentation.Endpoints.Approvals.DocumentType;

using Evolith.Ums.Application.Approvals.DocumentType.Commands;
using Evolith.Ums.Application.Approvals.DocumentType.DTOs;
using Evolith.Ums.Application.Approvals.DocumentType.Queries;
using Evolith.Ums.Application.Common;
using Evolith.Ums.Presentation.Endpoints.Common;

public static class DocumentTypeEndpoints
{
    public static IEndpointRouteBuilder MapDocumentTypeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/document-types").WithTags("DocumentTypes");

        group.MapGet("/", async (
            [AsParameters] TenantPagedQueryParameters query,
            IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllDocumentTypesQuery(
                query.Page, query.PageSize, query.Search,
                query.Criteria, query.SortBy, query.SortOrder, query.TenantId), ct);
            return result.ToOk(context);
        }).WithName("GetAllDocumentTypes").Produces<PagedResult<DocumentTypeDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (CreateDocumentTypeCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/document-types/{r.DocumentTypeId}", context);
        }).WithName("CreateDocumentType").Produces<CreateDocumentTypeResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{documentTypeId:guid}", async (Guid documentTypeId, UpdateDocumentTypeCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command with { DocumentTypeId = documentTypeId }, ct);
            return result.ToNoContent(context);
        }).WithName("UpdateDocumentType")
          .WithSummary("Update document type name and description")
          .Produces(StatusCodes.Status204NoContent)
          .ProducesProblem(StatusCodes.Status400BadRequest)
          .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
