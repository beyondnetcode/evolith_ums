using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Evolith.Ums.Infrastructure.Hosting;

// G-094 (endurece ADR-UMS-096 / G-093): el EFECTO de la promoción de rol IGA —reasignar Profile.RoleId—
// ya NO se aplica por un manejador in-process best-effort (INotificationHandler despachado
// post-commit), cuyo fallo dejaba la promoción Executed sin privilegio aplicado y solo con una
// advertencia en log (fallo silencioso, riesgo de cumplimiento). Ahora llega por el Transactional
// Outbox de MassTransit: ExecuteRolePromotionCommandHandler publica
// RolePromotionExecutedIntegrationEvent dentro de la transacción que confirma Execute, y este
// consumidor lo aplica con ENTREGA GARANTIZADA (reintentos + dead-letter, ver
// RolePromotionRoleAssignmentConsumerDefinition). Calca el patrón de AuditTrailPersistenceConsumer.
//
// La reasignación se hace en la PROPIA transacción del agregado Profile (Autorización), separada de
// la de RolePromotionRequest — respeta D-016 (un agregado por transacción). Es idempotente ante
// reentrega: tras aplicarse, el perfil ya no tiene el rol origen (CurrentRoleId), de modo que la
// regla de selección deja de encontrarlo y el reconsumo termina sin efecto.

/// <summary>
/// Aplica el cambio de rol del usuario objetivo cuando una promoción IGA se ejecuta
/// (<see cref="RolePromotionExecutedIntegrationEvent"/>).
///
/// Regla de selección de perfil (ADR-UMS-096): se reasignan los perfiles ACTIVOS del usuario objetivo,
/// en el inquilino de la solicitud, cuyo rol actual coincide con el rol origen de la promoción
/// (<c>CurrentRoleId</c>). En el caso normal es exactamente uno. El modelo de promoción no lleva
/// dimensión de sucursal (<c>BranchId</c>): un usuario con varios perfiles del mismo rol en distintas
/// sucursales verá reasignados TODOS (la promoción eleva «ese rol» del usuario).
///
/// Semántica de fallo (G-094): si un <c>ChangeRole</c> falla se LANZA excepción para que MassTransit
/// reintente y, agotados los reintentos, mueva el mensaje a la dead-letter — el efecto nunca se
/// descarta en silencio. Si no hay perfil coincidente no hay sujeto sobre el que actuar: se registra
/// advertencia y se confirma el mensaje (reintentar no encontraría un perfil inexistente); el efecto
/// es reconstruible (misma decisión que ADR-UMS-096).
/// </summary>
public sealed class RolePromotionRoleAssignmentConsumer(
    IProfileRepository profileRepository,
    ILogger<RolePromotionRoleAssignmentConsumer> logger)
    : IConsumer<RolePromotionExecutedIntegrationEvent>
{
    public Task Consume(ConsumeContext<RolePromotionExecutedIntegrationEvent> context)
        => ApplyRoleAssignmentAsync(context.Message, context.CancellationToken);

    /// <summary>
    /// Núcleo del efecto, expuesto para pruebas aisladas del consumidor (dado el evento, aplica la
    /// reasignación; ante un <c>ChangeRole</c> fallido lanza y no confirma éxito).
    /// </summary>
    public async Task ApplyRoleAssignmentAsync(
        RolePromotionExecutedIntegrationEvent message,
        CancellationToken cancellationToken)
    {
        var profiles = await profileRepository.GetByUserIdAsync(message.TargetUserId, cancellationToken);

        var toReassign = profiles
            .Where(p => p.TenantId.GetValue() == message.TenantId
                        && p.IsActive
                        && p.RoleId.GetValue() == message.CurrentRoleId)
            .ToList();

        if (toReassign.Count == 0)
        {
            logger.LogWarning(
                "G-094: promoción {RequestId} ejecutada, pero el usuario objetivo {TargetUserId} no tiene "
                + "perfil ACTIVO con el rol origen {CurrentRoleId} en el inquilino {TenantId}; no se reasignó "
                + "ningún rol. El efecto es reconstruible.",
                message.RequestId, message.TargetUserId, message.CurrentRoleId, message.TenantId);
            return;
        }

        var actor = ActorId.Create(message.ExecutorId.ToString());
        var newRoleId = RoleId.Load(message.TargetRoleId);

        foreach (var profile in toReassign)
        {
            var result = profile.ChangeRole(newRoleId, actor);
            if (result.IsFailure)
            {
                // G-094: NO silenciar. Lanzar hace que MassTransit reintente y, agotados los intentos,
                // envíe el mensaje a la dead-letter — el efecto de la promoción no se descarta.
                throw new InvalidOperationException(
                    $"G-094: no se pudo reasignar el rol del perfil {profile.GetId().GetValue()} "
                    + $"(promoción {message.RequestId}): {result.Error}");
            }

            await profileRepository.UpdateAsync(profile, cancellationToken);
        }

        // D-016: transacción propia del agregado Profile, separada de la de RolePromotionRequest.
        await profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        logger.LogInformation(
            "G-094: promoción {RequestId} aplicada — {Count} perfil(es) del usuario {TargetUserId} reasignado(s) "
            + "del rol {CurrentRoleId} al {TargetRoleId}.",
            message.RequestId, toReassign.Count, message.TargetUserId, message.CurrentRoleId, message.TargetRoleId);
    }
}

/// <summary>
/// G-094: política de reintentos explícita para <see cref="RolePromotionRoleAssignmentConsumer"/>.
/// <c>ConfigureEndpoints</c> no aplica reintentos por defecto, así que ante un fallo transitorio
/// (BD, concurrencia) el mensaje iría directo a la dead-letter. Con backoff exponencial se reintenta
/// varias veces y solo entonces se descarta a <c>_error</c> para inspección — nunca en silencio.
/// Auto-registrada por <c>AddConsumers(Assembly.GetExecutingAssembly())</c> junto al consumidor.
/// </summary>
public sealed class RolePromotionRoleAssignmentConsumerDefinition
    : ConsumerDefinition<RolePromotionRoleAssignmentConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<RolePromotionRoleAssignmentConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromMilliseconds(200),
                maxInterval: TimeSpan.FromSeconds(10),
                intervalDelta: TimeSpan.FromSeconds(1)));
    }
}
