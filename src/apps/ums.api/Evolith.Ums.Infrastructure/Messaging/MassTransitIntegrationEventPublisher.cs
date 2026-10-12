using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Events;
using MassTransit;

namespace Evolith.Ums.Infrastructure.Messaging;

/// <summary>
/// ADR-0098 D7 / opción F: publica eventos de integración por el bus de MassTransit. Bajo kind/prod
/// el <see cref="IPublishEndpoint"/> con ámbito de petición está respaldado por el bus-outbox de
/// <c>UmsPlatformDbContext</c> (<c>UseBusOutbox()</c>): el mensaje se estaciona con el cambio del
/// agregado y se entrega POST-commit. Es la frontera explícita entre el dominio (en proceso) y el
/// transporte inter-sistema; ningún evento de dominio crudo la cruza.
/// </summary>
public sealed class MassTransitIntegrationEventPublisher(
    IPublishEndpoint publishEndpoint,
    IFunctionalTransaction functionalTransaction)
    : IIntegrationEventPublisher
{
    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        functionalTransaction.RecordEffect("message.publish", "outbox", integrationEvent.GetType().Name, EffectReversibility.PendingCompensation);
        return publishEndpoint.Publish(integrationEvent, integrationEvent.GetType(), cancellationToken);
    }
}
