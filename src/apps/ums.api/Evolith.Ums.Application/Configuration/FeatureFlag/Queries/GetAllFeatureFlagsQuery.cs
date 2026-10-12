using Evolith.Ums.Application.Configuration.FeatureFlag.DTOs;

namespace Evolith.Ums.Application.Configuration.FeatureFlag.Queries;

public sealed record GetAllFeatureFlagsQuery(
    int Page = QueryDefaults.Page,
    int PageSize = QueryDefaults.PageSize,
    string? Search = null,
    string? Criteria = null,
    string? Status = null,
    string? SortBy = null,
    string? SortOrder = null,
    string? FlagType = null,
    string? LinkedResourceType = null) : IQuery<PagedResult<FeatureFlagDto>>;
