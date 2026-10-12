using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Commands;

using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Configuration.FeatureFlag;
using Evolith.Ums.Domain.Enums;

public sealed class CreateFeatureFlagCommandHandler : ICommandHandler<CreateFeatureFlagCommand, CreateFeatureFlagResponse>
{
    private readonly IFeatureFlagRepository _repository;
    private readonly IUserContext _userContext;

    public CreateFeatureFlagCommandHandler(IFeatureFlagRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_flags", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateFeatureFlagResponse>> Handle(CreateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreateFeatureFlagResponse>.Failure("Authenticated user is required.");
        }

        var existingFlag = await _repository.GetBySystemSuiteAndCodeAsync(request.SystemSuiteId, request.FlagCode, cancellationToken);
        if (existingFlag is not null)
        {
            return Result<CreateFeatureFlagResponse>.Failure("Feature flag code already exists.");
        }

        var flagType = DomainEnumerationParser.FromName<FlagType>(request.FlagType);
        if (flagType is null)
        {
            return Result<CreateFeatureFlagResponse>.Failure("Invalid feature flag type.");
        }

        var linkedResourceType = string.IsNullOrWhiteSpace(request.LinkedResourceType)
            ? null
            : DomainEnumerationParser.FromName<LinkedResourceType>(request.LinkedResourceType);

        if (!string.IsNullOrWhiteSpace(request.LinkedResourceType) && linkedResourceType is null)
        {
            return Result<CreateFeatureFlagResponse>.Failure("Invalid linked resource type.");
        }

        var result = FeatureFlag.Create(
            IdValueObject.Load(request.SystemSuiteId),
            request.TenantId.HasValue ? IdValueObject.Load(request.TenantId.Value) : null,
            request.FlagCode.Trim(),
            flagType,
            request.FlagTargets.Trim(),
            linkedResourceType,
            request.LinkedResourceId.HasValue ? IdValueObject.Load(request.LinkedResourceId.Value) : null,
            request.RolloutPercentage,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return Result<CreateFeatureFlagResponse>.Failure(result.Error);
        }

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateFeatureFlagResponse>.Success(new CreateFeatureFlagResponse(result.Value.Props.Id.GetValue()));
    }
}
