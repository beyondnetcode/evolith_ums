using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Configuration;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Queries;

public sealed class GetAllFeatureFlagsQueryHandler : IQueryHandler<GetAllFeatureFlagsQuery, PagedResult<FeatureFlagDto>>
{
    private readonly IFeatureFlagRepository _repository;
    private readonly ISystemSuiteRepository _systemSuiteRepository;
    private readonly ITenantContext? _tenantContext;

    public GetAllFeatureFlagsQueryHandler(
        IFeatureFlagRepository repository,
        ISystemSuiteRepository systemSuiteRepository,
        ITenantContext? tenantContext = null)
    {
        _repository = repository;
        _systemSuiteRepository = systemSuiteRepository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_flags", ActionCode = "view")]
    public async Task<Result<PagedResult<FeatureFlagDto>>> Handle(GetAllFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var search = NormalizeSearch(request.Search);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "flagCode").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var flagType = NormalizeSearch(request.FlagType);
        var linkedResourceType = NormalizeSearch(request.LinkedResourceType);

        // Feature flags are tenant-scoped; internal admins see all flags (null = no filter)
        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true)
            ? (Guid?)null
            : _tenantContext?.OrganizationId;

        var flags = await _repository.GetAllAsync(effectiveTenantId, cancellationToken);
        var suites = await _systemSuiteRepository.GetAllAsync(effectiveTenantId, cancellationToken);
        var suiteLookup = suites.ToDictionary(
            suite => suite.Props.Id.GetValue(),
            suite => new
            {
                Code = suite.Props.Code.GetValue(),
                Name = suite.Props.Name.GetValue(),
            });

        var query = flags.Select(flag =>
        {
            suiteLookup.TryGetValue(flag.Props.SystemSuiteId.GetValue(), out var suite);

            return new FeatureFlagDto(
                flag.Props.Id.GetValue(),
                flag.Props.SystemSuiteId.GetValue(),
                suite?.Code ?? string.Empty,
                suite?.Name ?? string.Empty,
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
                    c.CreatedAtUtc)).ToList());
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(flag =>
                flag.FlagCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                || flag.FlagTargets.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (flag.LinkedResourceType?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(flag => string.Equals(flag.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(flagType))
        {
            query = query.Where(flag => string.Equals(flag.FlagType, flagType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(linkedResourceType))
        {
            query = query.Where(flag => string.Equals(flag.LinkedResourceType, linkedResourceType, StringComparison.OrdinalIgnoreCase));
        }

        query = (sortBy, sortOrder) switch
        {
            ("status", "desc") => query.OrderByDescending(flag => flag.Status).ThenByDescending(flag => flag.FlagCode),
            ("status", _) => query.OrderBy(flag => flag.Status).ThenBy(flag => flag.FlagCode),
            ("flagtype", "desc") => query.OrderByDescending(flag => flag.FlagType).ThenByDescending(flag => flag.FlagCode),
            ("flagtype", _) => query.OrderBy(flag => flag.FlagType).ThenBy(flag => flag.FlagCode),
            ("linkedresourcetype", "desc") => query.OrderByDescending(flag => flag.LinkedResourceType).ThenByDescending(flag => flag.FlagCode),
            ("linkedresourcetype", _) => query.OrderBy(flag => flag.LinkedResourceType).ThenBy(flag => flag.FlagCode),
            ("rolloutpercentage", "desc") => query.OrderByDescending(flag => flag.RolloutPercentage).ThenByDescending(flag => flag.FlagCode),
            ("rolloutpercentage", _) => query.OrderBy(flag => flag.RolloutPercentage).ThenBy(flag => flag.FlagCode),
            ("flagcode", "desc") => query.OrderByDescending(flag => flag.FlagCode),
            _ => query.OrderBy(flag => flag.FlagCode),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<FeatureFlagDto>>.Success(new PagedResult<FeatureFlagDto>(paged, page, pageSize, totalItems, totalPages));
    }
}
