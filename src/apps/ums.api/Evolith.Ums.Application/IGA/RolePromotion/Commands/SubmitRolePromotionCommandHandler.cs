namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

using Evolith.Ums.Application.IGA.Common;
using Evolith.Ums.Application.IGA.Services;
using Evolith.Ums.Domain.IGA;

public sealed class SubmitRolePromotionCommandHandler : ICommandHandler<SubmitRolePromotionCommand>
{
    private readonly IRolePromotionRequestRepository _repository;
    private readonly IRiskScoreCalculator _riskScoreCalculator;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserContext _userContext;

    public SubmitRolePromotionCommandHandler(
        IRolePromotionRequestRepository repository,
        IRiskScoreCalculator riskScoreCalculator,
        ITenantScopePolicy tenantScopePolicy,
        IUserContext userContext)
    {
        _repository = repository;
        _riskScoreCalculator = riskScoreCalculator;
        _tenantScopePolicy = tenantScopePolicy;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_role_promotions", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(SubmitRolePromotionCommand request, CancellationToken cancellationToken)
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

        // SoD: quien envía (calcula y congela el riesgo) no puede ser el usuario objetivo.
        var sod = IgaHandlerGuards.EnsureSegregation(
            actor.Value,
            (entity.TargetUserId.GetValue(), "El usuario objetivo no puede enviar su propia promoción (segregación de funciones)."));
        if (sod.IsFailure)
        {
            return Result.Failure(sod.Error);
        }

        var assessment = await _riskScoreCalculator.CalculateAsync(
            new RolePromotionRiskContext(
                entity.TenantId.GetValue(),
                entity.TargetUserId.GetValue(),
                entity.CurrentRoleId.GetValue(),
                entity.TargetRoleId.GetValue()),
            cancellationToken);
        if (assessment.IsFailure)
        {
            return Result.Failure(assessment.Error);
        }

        var riskScoreResult = RiskScore.Create(assessment.Value.Score);
        if (riskScoreResult.IsFailure)
        {
            return Result.Failure(riskScoreResult.Error);
        }

        var submitResult = entity.Submit(riskScoreResult.Value, ActorId.Create(_userContext.UserId));
        if (submitResult.IsFailure)
        {
            return submitResult;
        }

        await _repository.UpdateAsync(entity, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
