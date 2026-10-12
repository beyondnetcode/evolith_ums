namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

using Evolith.Ums.Application.IGA.Common;
using Evolith.Ums.Domain.IGA;

public sealed class ManagerRejectRolePromotionCommandHandler
    : ICommandHandler<ManagerRejectRolePromotionCommand>
{
    private readonly IRolePromotionRequestRepository _repository;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserContext _userContext;

    public ManagerRejectRolePromotionCommandHandler(
        IRolePromotionRequestRepository repository,
        ITenantScopePolicy tenantScopePolicy,
        IUserContext userContext)
    {
        _repository = repository;
        _tenantScopePolicy = tenantScopePolicy;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "approve_role_promotions", ActionCode = "approve")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(
        ManagerRejectRolePromotionCommand request,
        CancellationToken cancellationToken)
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

        // SoD (INV-RPR3): quien decide el rechazo no puede ser el objetivo ni el solicitante.
        var sod = IgaHandlerGuards.EnsureSegregation(
            actor.Value,
            (entity.TargetUserId.GetValue(), "El aprobador no puede ser el usuario objetivo (segregación de funciones)."),
            (entity.RequesterId.GetValue(), "El aprobador no puede ser el solicitante (segregación de funciones)."));
        if (sod.IsFailure)
        {
            return Result.Failure(sod.Error);
        }

        var result = entity.ManagerReject(UserId.Load(actor.Value), request.Reason, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
