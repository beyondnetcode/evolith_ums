namespace Evolith.Ums.Presentation.Endpoints.Approvals.DocumentType.Queries;

using Evolith.Ums.Application.Approvals.DocumentType.DTOs;
using Evolith.Ums.Application.Approvals.DocumentType.Queries;

public static class DocumentTypeQueryEndpoints
{
    public static IEndpointRouteBuilder MapDocumentTypeQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/document-types").WithTags("DocumentTypes - Queries");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDocumentTypeByIdQuery(id), ct);
            return result.ToOk(context);
        }).WithName("GetDocumentTypeById").Produces<DocumentTypeDto>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
