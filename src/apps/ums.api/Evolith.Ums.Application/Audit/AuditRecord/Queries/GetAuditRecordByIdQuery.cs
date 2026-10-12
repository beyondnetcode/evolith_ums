using Evolith.Ums.Application.Audit.AuditRecord.DTOs;

namespace Evolith.Ums.Application.Audit.AuditRecord.Queries;

public sealed record GetAuditRecordByIdQuery(Guid AuditRecordId) : IQuery<AuditRecordDto>;
