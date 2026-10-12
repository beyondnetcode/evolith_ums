namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

using Evolith.Ums.Application.IGA.Common;
using Evolith.Ums.Domain.Events;
using Evolith.Ums.Domain.IGA;

public sealed class ExecuteRolePromotionCommandHandler : ICommandHandler<ExecuteRolePromotionCommand>
{
    private readonly IRolePromotionRequestRepository _repository;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserContext _userContext;
    private readonly IIntegrationEventPublisher _integrationEventPublisher;

    public ExecuteRolePromotionCommandHandler(
        IRolePromotionRequestRepository repository,
        ITenantScopePolicy tenantScopePolicy,
        IUserContext userContext,
        IIntegrationEventPublisher integrationEventPublisher)
    {
        _repository = repository;
        _tenantScopePolicy = tenantScopePolicy;
        _userContext = userContext;
        _integrationEventPublisher = integrationEventPublisher;
    }

    [AuthorizationAspect(ResourceCode = "execute_role_promotions", ActionCode = "execute")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ExecuteRolePromotionCommand request, CancellationToken cancellationToken)
    {
        var actor = IgaHandlerGuards.RequireAuthenticatedUser(_userContext);
        if (actor.IsFailure)
        {
            return Result.Failure(actor.Error);
        }

        var entity = await _repository.GetByIdAsync(request.RolePromotionRequestId, cancellationToken);
        if (entity is null)
        {
            return Result.Failure(DomainErrors.IGA.RolePromotionRequestNotFound);
        }

        var scope = IgaHandlerGuards.EnsureTenantInScope(_tenantScopePolicy, entity.TenantId.GetValue());
        if (scope.IsFailure)
        {
            return Result.Failure(scope.Error);
        }

        // SoD (INV-RPR3 endurecida, ADR-UMS-096): el ejecutor no puede ser el objetivo, el aprobador
        // ni el revisor de seguridad (espejo de VerifyRolePromotionCommandHandler).
        var sod = IgaHandlerGuards.EnsureSegregation(
            actor.Value,
            (entity.TargetUserId.GetValue(), "El ejecutor no puede ser el usuario objetivo (segregación de funciones)."),
            (entity.ApproverId?.GetValue(), "El ejecutor no puede ser el aprobador (segregación de funciones)."),
            (entity.SecurityReviewerId?.GetValue(), "El ejecutor no puede ser el revisor de seguridad (segregación de funciones)."));
        if (sod.IsFailure)
        {
            return Result.Failure(sod.Error);
        }

        var result = entity.Execute(UserId.Load(actor.Value), ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(entity, cancellationToken);

        // G-094: el EFECTO de la promoción (reasignar el rol del perfil objetivo) se entrega con
        // GARANTÍA por el Transactional Outbox de MassTransit, no por un handoff in-process
        // best-effort (G-093). Se publica el evento de integración ANTES de SaveEntitiesAsync: bajo
        // kind/prod el bus-outbox EF estaciona el mensaje en el MISMO change set del agregado y lo
        // confirma atómicamente con el cambio a Executed; el servicio de entrega lo despacha
        // POST-commit al RolePromotionRoleAssignmentConsumer (reintentos + dead-letter). En dev/tests
        // (bus en memoria, sin outbox EF) el publish entrega directo al consumidor en proceso. Así el
        // efecto nunca se pierde en silencio si el consumidor falla.
        await _integrationEventPublisher.PublishAsync(
            new RolePromotionExecutedIntegrationEvent(
                entity.TenantId.GetValue(),
                entity.GetId().GetValue(),
                entity.TargetUserId.GetValue(),
                entity.CurrentRoleId.GetValue(),
                entity.TargetRoleId.GetValue(),
                actor.Value),
            cancellationToken);

        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
