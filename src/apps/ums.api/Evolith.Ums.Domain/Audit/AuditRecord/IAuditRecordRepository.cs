namespace Evolith.Ums.Domain.Audit.AuditRecord;

using BeyondNetCode.Shell.Ddd.Interfaces;

public interface IAuditRecordRepository : IRepository<AuditRecord>
{
    Task<AuditRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AppendAsync(AuditRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<AuditRecord>> QueryByEntityAsync(Guid entityId, string entityType, Guid rootTenantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<AuditRecord>> QueryByActorAsync(Guid actorId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<IReadOnlyList<AuditRecord>> QueryByEventTypeAsync(string eventType, Guid rootTenantId, DateTime from, DateTime to, CancellationToken ct = default);
}
