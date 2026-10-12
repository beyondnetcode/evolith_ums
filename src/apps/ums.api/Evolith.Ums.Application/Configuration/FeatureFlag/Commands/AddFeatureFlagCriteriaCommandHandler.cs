using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

using Evolith.Ums.Domain.Configuration;

public sealed class AddFeatureFlagCriteriaCommandHandler : ICommandHandler<AddFeatureFlagCriteriaCommand, AddFeatureFlagCriteriaResponse>
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IUserContext _userContext;

    public AddFeatureFlagCriteriaCommandHandler(IFeatureFlagRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_flags", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<AddFeatureFlagCriteriaResponse>> Handle(AddFeatureFlagCriteriaCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<AddFeatureFlagCriteriaResponse>.Failure("Authenticated user is required.");
        }

        var featureFlag = await _repository.GetByIdAsync(request.FeatureFlagId, cancellationToken);
        if (featureFlag is null)
        {
            return Result<AddFeatureFlagCriteriaResponse>.Failure("Feature flag was not found.");
        }

        var result = featureFlag.AddCriteria(
            request.CriteriaType.Trim(),
            request.Operator.Trim(),
            request.Value.Trim(),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return Result<AddFeatureFlagCriteriaResponse>.Failure(result.Error);
        }

        await _repository.UpdateAsync(featureFlag, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        var criteriaId = featureFlag.Criteria.Last().Props.Id.GetValue();
        return Result<AddFeatureFlagCriteriaResponse>.Success(new AddFeatureFlagCriteriaResponse(criteriaId));
    }
}
