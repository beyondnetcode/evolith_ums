namespace Evolith.Ums.Domain.Events;

/// <summary>
/// ADR-0098 D7 / ADR-0097 D2: marca un evento de <b>INTEGRACIÓN</b> — contrato explícito y aparte
/// del modelo de dominio, destinado a cruzar la frontera del sistema (bróker inter-sistema).
///
/// A diferencia de un <see cref="BeyondNetCode.Shell.Ddd.Interfaces.IDomainEvent"/> —que se maneja
/// EN PROCESO y <b>nunca</b> se publica crudo al transporte (D7/D9.6)— un evento de integración se
/// versiona, es retrocompatible y se traduce explícitamente desde el dominio. Solo los tipos que
/// implementan esta interfaz salen al bróker, y únicamente a través de
/// <c>IIntegrationEventPublisher</c> (respaldado por el Transactional Outbox).
///
/// Hoy UMS no emite eventos de integración salientes (los handoffs vigentes —revocación de tokens,
/// proyecciones de lectura— son intra-servicio y se resuelven en proceso). Esta marca fija la
/// frontera para cuando exista un consumidor inter-sistema real, sin reabrir la fuga D7.
/// </summary>
public interface IIntegrationEvent
{
}
