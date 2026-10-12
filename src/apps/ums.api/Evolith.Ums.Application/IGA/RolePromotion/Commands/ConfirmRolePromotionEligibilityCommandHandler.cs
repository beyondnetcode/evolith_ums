namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

using Evolith.Ums.Application.IGA.Common;
using Evolith.Ums.Domain.IGA;

/// <summary>
/// Handler de confirmación de elegibilidad (fail-closed). Consulta el <c>RoleMaturityStatus</c>
/// del usuario objetivo en su rol actual y ejecuta <c>EvaluateEligibility</c>. Cualquier condición
/// no cumplida —incluida la ausencia del estado de madurez— se traduce en <em>no elegible</em>, con
/// lo que la promoción pasa a <c>Rejected</c> y nunca avanza (INV-RPR4).
/// El instante de evaluación se inyecta desde la aplicación (<c>DateTime.UtcNow</c>).
/// </summary>
public sealed class ConfirmRolePromotionEligibilityCommandHandler
    : ICommandHandler<ConfirmRolePromotionEligibilityCommand>
{
    private readonly IRolePromotionRequestRepository _repository;
    private readonly IRoleMaturityStatusRepository _maturityRepository;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserContext _userContext;

    public ConfirmRolePromotionEligibilityCommandHandler(
        IRolePromotionRequestRepository repository,
        IRoleMaturityStatusRepository maturityRepository,
        ITenantScopePolicy tenantScopePolicy,
        IUserContext userContext)
    {
        _repository = repository;
        _maturityRepository = maturityRepository;
        _tenantScopePolicy = tenantScopePolicy;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_role_promotions", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(
        ConfirmRolePromotionEligibilityCommand request,
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

        var actorId = ActorId.Create(_userContext.UserId);

        // Fuente de verdad de elegibilidad: el RoleMaturityStatus del objetivo en su rol actual.
        var maturity = await _maturityRepository.GetByUserAndRoleAsync(
            entity.TenantId.GetValue(),
            entity.TargetUserId.GetValue(),
            entity.CurrentRoleId.GetValue(),
            cancellationToken);

        var isEligible = false;
        if (maturity is not null)
        {
            // EvaluateEligibility muta el agregado sólo en éxito; sólo entonces se persiste.
            var eligibility = maturity.EvaluateEligibility(DateTime.UtcNow, actorId);
            isEligible = eligibility.IsSuccess;
            if (isEligible)
            {
                await _maturityRepository.UpdateAsync(maturity, cancellationToken);
            }
        }

        var confirmResult = entity.ConfirmEligibility(isEligible, actorId);
        if (confirmResult.IsFailure)
        {
            return confirmResult;
        }

        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
