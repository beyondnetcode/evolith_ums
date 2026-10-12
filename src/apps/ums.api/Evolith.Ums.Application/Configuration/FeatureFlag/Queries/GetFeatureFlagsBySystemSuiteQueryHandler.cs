using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Configuration;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Queries;

public sealed class GetFeatureFlagsBySystemSuiteQueryHandler : IQueryHandler<GetFeatureFlagsBySystemSuiteQuery, IReadOnlyList<FeatureFlagDto>>
{
    private readonly IFeatureFlagRepository _repository;
    private readonly ISystemSuiteRepository _systemSuiteRepository;

    public GetFeatureFlagsBySystemSuiteQueryHandler(
        IFeatureFlagRepository repository,
        ISystemSuiteRepository systemSuiteRepository)
    {
        _repository = repository;
        _systemSuiteRepository = systemSuiteRepository;
    }

    [AuthorizationAspect(ResourceCode = "view_flags", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<IReadOnlyList<FeatureFlagDto>>> Handle(GetFeatureFlagsBySystemSuiteQuery request, CancellationToken cancellationToken)
    {
        var flags = await _repository.GetBySystemSuiteIdAsync(request.SystemSuiteId, cancellationToken);
        var systemSuite = await _systemSuiteRepository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        var systemSuiteCode = systemSuite?.Props.Code.GetValue() ?? string.Empty;
        var systemSuiteName = systemSuite?.Props.Name.GetValue() ?? string.Empty;

        var dtos = flags.Select(flag => new FeatureFlagDto(
            flag.Props.Id.GetValue(),
            flag.Props.SystemSuiteId.GetValue(),
            systemSuiteCode,
            systemSuiteName,
            flag.Props.FlagCode,
            flag.Props.FlagType.Name,
            flag.Props.FlagTargets,
            flag.Props.Status.Name,
            flag.Props.LinkedResourceType?.Name,
            flag.Props.LinkedResourceId?.GetValue(),
            flag.Props.RolloutPercentage,
            flag.Criteria.Select(c => new FeatureFlagCriteriaDto(
                c.Props.Id.GetValue(),
                c.CriteriaType,
                c.Operator,
                c.Value,
                c.CreatedAtUtc)).ToList())).ToList();

        return Result<IReadOnlyList<FeatureFlagDto>>.Success(dtos);
    }
}
