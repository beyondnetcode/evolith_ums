namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Records authentication events in the append-only audit log.
/// Uses IAuditRecordRepository.AppendAsync() — no new table required.
/// </summary>
public interface IAuthAuditService
{
    Task RecordAuthEventAsync(AuthAuditEvent evt, CancellationToken cancellationToken = default);
}

/// <summary>Authentication event to be recorded in the audit trail.</summary>
public sealed record AuthAuditEvent(
    Guid    UserId,          // Guid.Empty when user could not be identified
    Guid    TenantId,        // Guid.Empty when tenant could not be identified
    string  TenantCode,
    string  AuthMethod,      // "Local" | "IDP"
    string  EventType,       // "Auth.Login.Success" | "Auth.Login.Failure" | "Auth.Login.IdpFailure"
    bool    Succeeded,
    string  ClientIp,
    string? FailureReason = null,
    string? IdpProvider   = null);
