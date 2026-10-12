namespace Evolith.Ums.Application.Common.Aop;

/// <summary>
/// Sumidero de la pista de auditoría automática (AUDIT-01..06, ADR-0016).
///
/// G-040 (residual arquitectural): la vía automática ya no escribe a un canal en memoria con
/// descarte silencioso (el antiguo <c>Channel.TryWrite</c> devolvía <c>false</c> al saturarse y
/// se ignoraba). El registro se encola por el <b>Transactional Outbox</b> de MassTransit
/// cableado en <c>UmsPlatformDbContext</c> (<c>UseBusOutbox()</c>), con entrega POST-commit al
/// consumidor que lo persiste de forma append-only. Un fallo al encolar se registra como error
/// alertable; nunca se descarta en silencio.
/// </summary>
public interface IAuditTrailSink
{
    Task PublishAsync(AuditTrailEntry entry, CancellationToken cancellationToken = default);
}
