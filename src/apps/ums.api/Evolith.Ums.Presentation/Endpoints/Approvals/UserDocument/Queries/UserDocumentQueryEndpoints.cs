namespace Evolith.Ums.Presentation.Endpoints.Approvals.UserDocument.Queries;

using Evolith.Ums.Application.Approvals.UserDocument.DTOs;
using Evolith.Ums.Application.Approvals.UserDocument.Queries;

public static class UserDocumentQueryEndpoints
{
    public static IEndpointRouteBuilder MapUserDocumentQueryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/user-documents").WithTags("UserDocuments - Queries");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetUserDocumentByIdQuery(id), ct);
            return result.ToOk(context);
        }).WithName("GetUserDocumentById").Produces<UserDocumentDto>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
