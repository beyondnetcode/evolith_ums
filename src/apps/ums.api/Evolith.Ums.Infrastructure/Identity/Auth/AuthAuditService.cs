using System.Text.Json;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Audit.AuditRecord;

namespace Evolith.Ums.Infrastructure.Identity.Auth;

/// <summary>
/// Records authentication events in the append-only AuditRecord table.
/// Uses IAuditRecordRepository.AppendAsync — no new table required.
/// </summary>
public sealed class AuthAuditService : IAuthAuditService
{
    private readonly IAuditRecordRepository _auditRepo;

    public AuthAuditService(IAuditRecordRepository auditRepo)
    {
        _auditRepo = auditRepo;
    }

    public async Task RecordAuthEventAsync(AuthAuditEvent evt, CancellationToken cancellationToken = default)
    {
        var whatChanged = JsonSerializer.Serialize(new
        {
            method     = evt.AuthMethod,
            tenantCode = evt.TenantCode,
            ip         = evt.ClientIp,
            result     = evt.Succeeded ? "Success" : "Failure",
            reason     = evt.FailureReason,
            idp        = evt.IdpProvider,
        });

        var affectedId   = evt.UserId   != Guid.Empty ? evt.UserId   : Guid.NewGuid();
        var rootTenantId = evt.TenantId != Guid.Empty ? evt.TenantId : Guid.NewGuid();

        var record = AuditRecord.Record(
            whoActed:           evt.UserId != Guid.Empty ? evt.UserId : Guid.NewGuid(),
            subjectType:        SubjectType.User,
            whatChanged:        whatChanged,
            eventType:          evt.EventType,
            auditResult:        evt.Succeeded ? AuditResult.Success : AuditResult.Failure,
            affectedEntityId:   affectedId,
            affectedEntityType: "UserAccount",
            rootTenantId:       rootTenantId);

        if (record.IsSuccess)
        {
            await _auditRepo.AppendAsync(record.Value, cancellationToken);
            // Auto-commit: los eventos Auth.* deben dejar traza siempre, incluso si el
            // flujo que los origina no cierra una unidad de trabajo (p. ej. logout, o un
            // login fallido que no persiste nada más). Persistir aquí lo garantiza.
            await _auditRepo.UnitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
