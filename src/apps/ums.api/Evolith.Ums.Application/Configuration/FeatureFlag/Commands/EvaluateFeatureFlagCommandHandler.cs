using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;

public sealed class EvaluateFeatureFlagCommandHandler : ICommandHandler<EvaluateFeatureFlagCommand, EvaluateFeatureFlagResponse>
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IFeatureFlagEvaluator _evaluator;

    public EvaluateFeatureFlagCommandHandler(
        IFeatureFlagRepository repository,
        IUserContext userContext,
        IFeatureFlagEvaluator evaluator)
    {
        _repository = repository;
        _userContext = userContext;
        _evaluator = evaluator;
    }

    [AuthorizationAspect(ResourceCode = "manage_flags", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<EvaluateFeatureFlagResponse>> Handle(EvaluateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<EvaluateFeatureFlagResponse>.Failure("Authenticated user is required.");
        }

        if (!Guid.TryParse(_userContext.UserId, out var evaluatedBy))
        {
            return Result<EvaluateFeatureFlagResponse>.Failure("Authenticated user id must be a valid GUID.");
        }

        var featureFlag = await _repository.GetByIdAsync(request.FeatureFlagId, cancellationToken);
        if (featureFlag is null)
        {
            return Result<EvaluateFeatureFlagResponse>.Failure("Feature flag was not found.");
        }

        var context = new EvaluationContext(
            TenantId: request.TenantId,
            BranchId: request.BranchId,
            ProfileId: request.ProfileId,
            RoleCode: request.RoleCode,
            Environment: request.Environment,
            CustomAttributes: request.CustomAttributes);

        var evaluation = featureFlag.Evaluate(evaluatedBy, context, _evaluator);
        if (evaluation.IsFailure)
        {
            return Result<EvaluateFeatureFlagResponse>.Failure(evaluation.Error);
        }

        await _repository.UpdateAsync(featureFlag, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<EvaluateFeatureFlagResponse>.Success(new EvaluateFeatureFlagResponse(
            featureFlag.Props.Id.GetValue(),
            featureFlag.Props.FlagCode,
            evaluation.Value.IsEnabled,
            evaluation.Value.MatchedCriteriaType,
            evaluation.Value.Reason));
    }
}
