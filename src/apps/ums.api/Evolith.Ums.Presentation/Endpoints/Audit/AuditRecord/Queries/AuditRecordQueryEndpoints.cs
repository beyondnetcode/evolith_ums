namespace Evolith.Ums.Presentation.Endpoints.Audit.AuditRecord.Queries;

using Evolith.Ums.Application.Audit.AuditRecord.DTOs;
using Evolith.Ums.Application.Audit.AuditRecord.Queries;

public static class AuditRecordQueryEndpoints
{
    public static IEndpointRouteBuilder MapAuditRecordQueryEndpoints(this IEndpointRouteBuilder app)
    {
        // G-040 (SEGURIDAD): lectura de auditoría solo para usuarios autenticados.
        var group = app.MapGroup("/audit-records")
            .WithTags("AuditRecords - Queries")
            .RequireAuthorization();

        group.MapGet("/{auditRecordId:guid}", async (Guid auditRecordId, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAuditRecordByIdQuery(auditRecordId), ct);
            return result.ToOk(context);
        })
        .WithName("GetAuditRecordById")
        .WithSummary("Get audit record by ID")
        .Produces<AuditRecordDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
