using Evolith.Ums.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Evolith.Ums.Infrastructure.Aop;

/// <summary>
/// G-040: encola el registro de auditoría automática por el <b>Transactional Outbox</b> de
/// MassTransit (ADR-0098 D6: efecto durable cuya pérdida es irreparable — no-repudiación).
/// Reemplaza el canal en memoria con descarte silencioso.
///
/// La auditoría se captura DESPUÉS del commit del caso de uso (el <c>AuditTrailAspect</c> es el
/// aspecto más interno, <c>Order=100</c>). Por eso:
///  - Publica por el <see cref="IPublishEndpoint"/> con ámbito de petición. Bajo kind/prod con
///    <c>UseBusOutbox()</c> ese endpoint escribe en la tabla de salida; el mensaje se materializa
///    al llamar <c>SaveChangesAsync</c> sobre el <see cref="UmsPlatformDbContext"/> de la petición
///    (flush) y el servicio de entrega lo despacha POST-commit al bróker; el consumidor lo
///    persiste append-only con idempotencia por el inbox EF.
///  - En dev/tests (bus en memoria, sin outbox EF) no hay <see cref="UmsPlatformDbContext"/>
///    registrado: el publish entrega directamente al consumidor en proceso y el flush se omite.
///  - Cualquier fallo se registra como error alertable; nunca se descarta (fin del
///    <c>TryWrite==false</c> silencioso).
/// </summary>
public sealed class AuditTrailOutboxSink(
    IPublishEndpoint publishEndpoint,
    IServiceProvider serviceProvider,
    ILogger<AuditTrailOutboxSink> logger,
    Evolith.Ums.Application.Common.Interfaces.IFunctionalTransaction functionalTransaction) : IAuditTrailSink
{
    public async Task PublishAsync(AuditTrailEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            // G-040 (FR-072): desinfecta la metadata ANTES de encolar — la traza es append-only e
            // inmutable (G-081), así que un secreto (hash/PIN/llave/token) que se filtrase quedaría
            // irreparable. Este sink es el único punto por el que pasan TODAS las emisiones automáticas
            // (AuditTrailAspect y ConfigurationAuditService), así que el saneo aquí las cubre a todas y,
            // al hacerlo antes del Publish, el secreto tampoco llega a la tabla de salida del outbox.
            var sanitizedEntry = entry with { Metadata = AuditMetadataSanitizer.Sanitize(entry.Metadata) };

            functionalTransaction.RecordEffect("message.publish", "outbox", $"AuditTrailEntry-{sanitizedEntry.EventType}", Evolith.Ums.Application.Common.Interfaces.EffectReversibility.PendingCompensation);
            await publishEndpoint.Publish(sanitizedEntry, cancellationToken);

            // Flush del bus-outbox: materializa el mensaje de salida en la conexión de la petición
            // para que el servicio de entrega lo despache POST-commit. En modos sin outbox EF
            // (dev/tests en memoria) el contexto de escritura no está registrado y el publish ya
            // entregó en proceso, así que se omite.
            var writeDbContext = serviceProvider.GetService<UmsPlatformDbContext>();
            if (writeDbContext is not null)
                await writeDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Sin descarte silencioso (G-040): la auditoría es no repudiable; el fallo se alerta.
            logger.LogError(ex,
                "Auditoría no confiable: fallo al encolar el registro de auditoría {EventType} para {AffectedEntityType}/{AffectedEntityId}. Evento alertable.",
                entry.EventType,
                entry.AffectedEntityType,
                entry.AffectedEntityId);
        }
    }
}
