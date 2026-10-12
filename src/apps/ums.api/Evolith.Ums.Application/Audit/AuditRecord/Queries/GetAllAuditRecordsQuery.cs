using Evolith.Ums.Application.Audit.AuditRecord.DTOs;

namespace Evolith.Ums.Application.Audit.AuditRecord.Queries;

public sealed record GetAllAuditRecordsQuery(
    int Page = 1,
    int PageSize = 20,
    string? EventType = null,
    Guid? ActorId = null,
    Guid? EntityId = null,
    string? EntityType = null,
    Guid? TenantId = null,
    DateTime? From = null,
    DateTime? To = null) : IQuery<PagedResult<AuditRecordDto>>;
