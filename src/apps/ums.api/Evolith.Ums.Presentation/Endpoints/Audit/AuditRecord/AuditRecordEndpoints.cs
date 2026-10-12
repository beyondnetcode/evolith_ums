namespace Evolith.Ums.Presentation.Endpoints.Audit.AuditRecord;

using Evolith.Ums.Application.Audit.AuditRecord.Commands;
using Evolith.Ums.Application.Audit.AuditRecord.DTOs;
using Evolith.Ums.Application.Audit.AuditRecord.Queries;
using Evolith.Ums.Application.Common;

public static class AuditRecordEndpoints
{
    public static IEndpointRouteBuilder MapAuditRecordEndpoints(this IEndpointRouteBuilder app)
    {
        // G-040 (SEGURIDAD): la auditoría es sensible; todos los endpoints exigen
        // autenticación. Sin esto la lectura y el registro eran anónimos.
        var group = app.MapGroup("/audit-records")
            .WithTags("AuditRecords")
            .RequireAuthorization();

        group.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? eventType,
            [FromQuery] Guid? actorId,
            [FromQuery] Guid? entityId,
            [FromQuery] string? entityType,
            [FromQuery] Guid? tenantId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAllAuditRecordsQuery(
                page <= 0 ? 1 : page,
                pageSize <= 0 ? 20 : pageSize,
                eventType, actorId, entityId, entityType, tenantId, from, to), ct);
            return result.ToOk(context);
        })
        .WithName("GetAllAuditRecords")
        .WithSummary("Get audit records using server-side pagination")
        .Produces<PagedResult<AuditRecordDto>>(StatusCodes.Status200OK);

        group.MapPost("/", async (RecordAuditCommand command, IMediator mediator, HttpContext context, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return result.ToCreated(r => $"/audit-records/{r.AuditRecordId}", context);
        })
        .WithName("RecordAudit")
        .WithSummary("Record a new audit entry")
        .Produces<RecordAuditResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
