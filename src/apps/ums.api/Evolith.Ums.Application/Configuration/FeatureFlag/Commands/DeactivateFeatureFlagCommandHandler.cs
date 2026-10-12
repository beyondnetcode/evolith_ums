
namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

using Evolith.Ums.Domain.Configuration;

public sealed class DeactivateFeatureFlagCommandHandler : ICommandHandler<DeactivateFeatureFlagCommand>
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IUserContext _userContext;

    public DeactivateFeatureFlagCommandHandler(IFeatureFlagRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_flags", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeactivateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required.");
        }

        var featureFlag = await _repository.GetByIdAsync(request.FeatureFlagId, cancellationToken);
        if (featureFlag is null)
        {
            return Result.Failure("Feature flag was not found.");
        }

        var result = featureFlag.Deactivate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(featureFlag, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
