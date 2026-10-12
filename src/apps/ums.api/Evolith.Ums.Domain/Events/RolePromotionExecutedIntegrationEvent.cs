namespace Evolith.Ums.Domain.Events;

/// <summary>
/// ADR-0098 D7 / G-094: evento de <b>INTEGRACIÓN</b> que materializa el EFECTO de una promoción de
/// rol IGA ejecutada (<c>RolePromotionRequest.Execute</c>) — la reasignación del rol del perfil del
/// usuario objetivo. A diferencia del evento de dominio homónimo
/// (<see cref="RolePromotionExecutedEvent"/>, señal de auditoría en proceso, D7/D9.6), este contrato
/// explícito viaja por el <b>Transactional Outbox</b> de MassTransit: se estaciona atómicamente con
/// el cambio de estado del agregado en <c>ExecuteRolePromotionCommandHandler</c> y se entrega
/// POST-commit con garantía (reintentos + dead-letter) a <c>RolePromotionRoleAssignmentConsumer</c>.
///
/// Reemplaza el handoff in-process best-effort (G-093, <c>RolePromotionRoleAssignmentHandler</c>),
/// cuyo fallo dejaba la promoción <c>Executed</c> sin aplicar el privilegio y solo con una
/// advertencia en log (fallo silencioso, riesgo de cumplimiento — G-094).
///
/// El payload es autocontenido —lleva objetivo y roles origen/destino— para que el consumidor
/// reasigne el <c>Profile</c> sin recargar la solicitud de promoción (D-016: un agregado por
/// transacción).
/// </summary>
public sealed record RolePromotionExecutedIntegrationEvent(
    Guid TenantId,
    Guid RequestId,
    Guid TargetUserId,
    Guid CurrentRoleId,
    Guid TargetRoleId,
    Guid ExecutorId) : IIntegrationEvent;
