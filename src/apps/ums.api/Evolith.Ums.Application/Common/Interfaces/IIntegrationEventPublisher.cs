using Evolith.Ums.Domain.Events;

namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// ADR-0098 D7: puerto para publicar eventos de <b>INTEGRACIÓN</b> explícitos hacia el bróker
/// inter-sistema, a través del Transactional Outbox. Es la ÚNICA vía por la que un mensaje sale del
/// proceso hacia el transporte. Los eventos de dominio se manejan en proceso (MediatR, post-commit)
/// y nunca usan este puerto: publicarlos crudos convertiría el modelo interno en contrato público
/// y congelaría el dominio (D7/D9.6).
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
